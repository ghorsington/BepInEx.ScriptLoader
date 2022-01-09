using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using BepInEx;
using BepInEx.IL2CPP;
using BepInEx.Logging;

namespace ScriptLoader
{
    [BepInPlugin("horse.coder.tools.scriptloader", "C# Script Loader", "1.3.0")]
    public class ScriptLoader : BasePlugin
    {
        private readonly string scriptsPath = Path.Combine(Paths.GameRootPath, "scripts");
        private Dictionary<string, ScriptInfo> availableScripts = new Dictionary<string, ScriptInfo>();
        private FileSystemWatcher fileSystemWatcher;
        private Assembly lastCompilationAssembly;
        private string lastCompilationHash;
        private LogTextWriter LogTextWriter;
        public override bool Unload()
        {
            fileSystemWatcher.EnableRaisingEvents = false;
            fileSystemWatcher.Dispose();
            return base.Unload();
        }
        public override void Load()
        {
            LogTextWriter = new LogTextWriter(Log);
            CompileScripts();

            fileSystemWatcher = new FileSystemWatcher(scriptsPath);
            fileSystemWatcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName;
            fileSystemWatcher.Filter = "*.cs";
            fileSystemWatcher.Changed += (sender, args) =>
            {
                Log.LogInfo("File " + Path.GetFileName(args.Name) + " changed. Recompiling...");
                CompileScripts();
            };
            fileSystemWatcher.Deleted += (sender, args) =>
            {
                Log.LogInfo("File " + Path.GetFileName(args.Name) + " removed. Recompiling...");
                CompileScripts();
            };
            fileSystemWatcher.Created += (sender, args) =>
            {
                Log.LogInfo("File " + Path.GetFileName(args.Name) + " created. Recompiling...");
                CompileScripts();
            };
            fileSystemWatcher.Renamed += (sender, args) =>
            {
                Log.LogInfo("File " + Path.GetFileName(args.Name) + " renamed. Recompiling...");
                CompileScripts();
            };
            fileSystemWatcher.EnableRaisingEvents = true;
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

            bool IsValidProcess(string scriptFile)
            {
                var si = availableScripts[scriptFile];

                if (si.ProcessFilters.Count == 0)
                    return true;
                return si.ProcessFilters.Any(p => string.Equals(p.ToLowerInvariant().Replace(".exe", ""), Paths.ProcessName,
                                                                StringComparison.InvariantCultureIgnoreCase));
            }

            bool UsesHarmonyWrapper(string scriptFile)
            {
                var text = File.ReadAllText(scriptFile);
                if (text.Contains("HarmonyWrapper") || text.Contains("BepInEx.Harmony"))
                {
                    Log.LogError("Skipping loading `" + scriptFile + "` because it references outdated HarmonyWrapper and BepInEx.Harmony. To fix this, refer to github.com/denikson/BepInEx.ScriptLoader#upgrading-to-1240");
                    return true;
                }
                return false;
            }

            var ignores = new HashSet<string>(File.ReadAllLines(ignoresPath).Select(s => s.Trim()));
            var scriptsToCompile = files.Where(f => !UsesHarmonyWrapper(f) && IsValidProcess(f) && !ignores.Contains(Path.GetFileName(f))).ToList();

            Log.LogInfo(
                "Found " + files.Length + " scripts to compile, skipping " + (files.Length - scriptsToCompile.Count) + " scripts because of `scriptignores` or process filters");

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
                Log.LogInfo("No changes detected! Skipping compilation!");
                return;
            }

            foreach (var scriptFile in scriptsToCompile)
            {
                if (!availableScripts.TryGetValue(scriptFile, out var info)) continue;
                foreach (var infoReference in info.References)
                    Assembly.LoadFile(infoReference);
            }

            var ass = MonoCompiler.Compile(scriptDict, LogTextWriter);

            if (ass == null)
            {
                Log.LogError("Skipping loading scripts because of errors above.");
                return;
            }

            if (lastCompilationAssembly != null)
                foreach (var type in lastCompilationAssembly.GetTypes())
                {
                    var method = type.GetMethods(BindingFlags.Static | BindingFlags.Public)
                        .FirstOrDefault(m => m.Name == "Unload" && m.GetParameters().Length == 0);

                    if (method == null)
                        continue;

                    Log.Log(LogLevel.Info, "Unloading " + type.Name);
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

                Log.Log(LogLevel.Info, "Running " + type.Name);
                method.Invoke(null, new object[0]);
            }
        }
    }

    internal class LogTextWriter : TextWriter
    {
        private readonly ManualLogSource Log;
        private readonly StringBuilder sb = new StringBuilder();

        public LogTextWriter(ManualLogSource Log)
        {
            this.Log = Log;
        }

        public override Encoding Encoding { get; } = Encoding.UTF8;

        public override void Write(char value)
        {
            if (value == '\n')
            {
                Log.Log(LogLevel.Info, sb.ToString());
                sb.Length = 0;
                return;
            }

            sb.Append(value);
        }
    }
}