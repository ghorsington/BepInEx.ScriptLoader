using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;

namespace ScriptLoader
{
    [BepInPlugin("horse.coder.tools.scriptloader", "C# Script Loader", "1.0")]
    public class ScriptLoader : BaseUnityPlugin
    {
        public void Awake()
        {
            DontDestroyOnLoad(this);

            if (!Directory.Exists("scripts"))
            {
                Directory.CreateDirectory("scripts");
                Destroy(this);
                return;
            }

            var files = Directory.GetFiles("scripts", "*.cs").Select(p => new CSharpFile(p)).ToList();
            var ass = MonoCompiler.Compile(files);
            Logger.Log(LogLevel.Info, $"Compiling {files.Count} files");

            if (ass == null)
            {
                Logger.Log(LogLevel.Error, "Failed to compile!");
                Destroy(this);
                return;
            }

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
}