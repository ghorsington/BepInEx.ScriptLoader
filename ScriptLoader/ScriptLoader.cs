using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using BepInEx;
using BepInEx.Logging;

namespace ScriptLoader
{
    [BepInPlugin("horse.coder.tools.scriptloader", "C# Script Loader", "1.0")]
    public class ScriptLoader : BaseUnityPlugin
    {
        private readonly string scriptsPath = Path.Combine(Paths.GameRootPath, "scripts");
        private Dictionary<string, ScriptInfo> availableScripts = new Dictionary<string, ScriptInfo>();
        private FileSystemWatcher fileSystemWatcher;
        private Assembly lastCompilationAssembly;
        private string lastCompilationHash;
        private LoggerTextWriter loggerTextWriter;
        private bool shouldRecompile;

        private void Awake()
        {
            DontDestroyOnLoad(this);
            loggerTextWriter = new LoggerTextWriter(Logger);
            CompileScripts();

            fileSystemWatcher = new FileSystemWatcher(scriptsPath);
            fileSystemWatcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName;
            fileSystemWatcher.Filter = "*.cs";
            fileSystemWatcher.Changed += (sender, args) =>
            {
                Logger.LogInfo($"File {args.Name} changed. Recompiling.");
                shouldRecompile = true;
            };
            fileSystemWatcher.Deleted += (sender, args) =>
            {
                Logger.LogInfo($"File {args.Name} removed. Recompiling.");
                shouldRecompile = true;
            };
            fileSystemWatcher.Created += (sender, args) =>
            {
                Logger.LogInfo($"File {args.Name} created. Recompiling.");
                shouldRecompile = true;
            };
            fileSystemWatcher.Renamed += (sender, args) =>
            {
                Logger.LogInfo($"File {args.Name} renamed. Recompiling.");
                shouldRecompile = true;
            };
            fileSystemWatcher.EnableRaisingEvents = true;
        }

        private void OnDestroy()
        {
            fileSystemWatcher.EnableRaisingEvents = false;
            fileSystemWatcher.Dispose();
        }

        private void Update()
        {
            if (!shouldRecompile)
                return;
            CompileScripts();
            shouldRecompile = false;
        }

        private void CompileScripts()
        {
            if (!Directory.Exists(scriptsPath))
            {
                Directory.CreateDirectory(scriptsPath);
                return;
            }

            var files = Directory.GetFiles(scriptsPath, "*.cs");
            availableScripts = files.ToDictionary(f => f, ScriptInfo.FromTextFile);

            var ignoresPath = Path.Combine(scriptsPath, "scriptignores");
            if (!File.Exists(ignoresPath))
                File.WriteAllText(ignoresPath, "");

            var ignores = new HashSet<string>(File.ReadAllLines(ignoresPath).Select(s => s.Trim()));
            var scriptsToCompile = files.Where(f => !ignores.Contains(Path.GetFileName(f))).ToList();

            Logger.LogInfo(
                $"Found {files.Length} scripts to compile, skipping {files.Length - scriptsToCompile.Count} scripts because of `scriptignores`");

            var md5 = MD5.Create();
            var scriptDict = new Dictionary<string, byte[]>();
            foreach (var scriptFile in scriptsToCompile)
            {
                var data = File.ReadAllBytes(scriptFile);
                md5.TransformBlock(data, 0, data.Length, null, 0);
                scriptDict[scriptFile] = data;
            }

            md5.TransformFinalBlock(new byte[0], 0, 0);
            var hash = Convert.ToBase64String(md5.Hash);
            
            if (hash == lastCompilationHash)
            {
                Logger.LogInfo("No changes detected! Skipping compilation!");
                return;
            }

            foreach (var scriptFile in scriptsToCompile)
            {
                if (!availableScripts.TryGetValue(scriptFile, out var info) || info == null) continue;
                foreach (var infoReference in info.References)
                    Assembly.LoadFile(infoReference);
            }

            var ass = MonoCompiler.Compile(scriptDict, loggerTextWriter);

            if (ass == null)
            {
                Logger.LogError("Skipping loading scripts because of errors above.");
                return;
            }

            if (lastCompilationAssembly != null)
                foreach (var type in lastCompilationAssembly.GetTypes())
                {
                    var method = type.GetMethods(BindingFlags.Static | BindingFlags.Public)
                        .FirstOrDefault(m => m.Name == "Finalize" && m.GetParameters().Length == 0);

                    if (method == null)
                        continue;

                    Logger.Log(LogLevel.Info, $"Finalizing {type.Name}");
                    method.Invoke(null, new object[0]);
                }

            lastCompilationAssembly = ass;
            lastCompilationHash = hash;

            foreach (var type in ass.GetTypes())
            {
                var method = type.GetMethods(BindingFlags.Static | BindingFlags.Public)
                    .FirstOrDefault(m => m.Name == "Main" && m.GetParameters().Length == 0);

                if (method == null)
                    continue;

                Logger.Log(LogLevel.Info, $"Running {type.Name}");
                method.Invoke(null, new object[0]);
            }
        }
    }

    internal class LoggerTextWriter : TextWriter
    {
        private readonly ManualLogSource logger;
        private readonly StringBuilder sb = new StringBuilder();

        public LoggerTextWriter(ManualLogSource logger)
        {
            this.logger = logger;
        }

        public override Encoding Encoding { get; } = Encoding.UTF8;

        public override void Write(char value)
        {
            if (value == '\n')
            {
                logger.Log(LogLevel.Info, sb.ToString());
                sb.Length = 0;
                return;
            }

            sb.Append(value);
        }
    }
}