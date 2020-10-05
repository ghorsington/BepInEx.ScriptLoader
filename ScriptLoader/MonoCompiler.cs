using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using BepInEx;
using Mono.CSharp;

namespace ScriptLoader
{
    public static class MonoCompiler
    {
        private static readonly HashSet<string> StdLib =
            new HashSet<string>(StringComparer.InvariantCultureIgnoreCase)
                {"mscorlib", "System.Core", "System", "System.Xml"};

        private static readonly HashSet<string> compiledAssemblies = new HashSet<string>();

        // Mimicked from https://github.com/kkdevs/Patchwork/blob/master/Patchwork/MonoScript.cs#L124
        public static Assembly Compile(Dictionary<string, byte[]> sources, TextWriter logger = null)
        {
            ReportPrinter reporter = logger == null ? new ConsoleReportPrinter() : new StreamReportPrinter(logger);
            Location.Reset();

            var dllName = $"compiled_{DateTime.Now.Ticks}";
            compiledAssemblies.Add(dllName);

            var ctx = CreateContext(reporter);
            ctx.Settings.SourceFiles.Clear();

            var i = 0;

            SeekableStreamReader GetFile(SourceFile file)
            {
                return new SeekableStreamReader(new MemoryStream(sources[file.OriginalFullPathName]), Encoding.UTF8);
            }

            foreach (var source in sources)
            {
                ctx.Settings.SourceFiles.Add(new SourceFile(Path.GetFileName(source.Key), source.Key, i, GetFile));
                i++;
            }

            var container = new ModuleContainer(ctx);

            RootContext.ToplevelTypes = container;
            Location.Initialize(ctx.Settings.SourceFiles);

            var session = new ParserSession {UseJayGlobalArrays = true, LocatedTokens = new LocatedToken[15000]};
            container.EnableRedefinition();

            foreach (var sourceFile in ctx.Settings.SourceFiles)
            {
                var stream = sourceFile.GetInputStream(sourceFile);
                var source = new CompilationSourceFile(container, sourceFile);
                source.EnableRedefinition();
                container.AddTypeContainer(source);
                var parser = new CSharpParser(stream, source, session);
                parser.parse();
            }

            var ass = new AssemblyDefinitionDynamic(container, dllName, $"{dllName}.dll");
            container.SetDeclaringAssembly(ass);

            var importer = new ReflectionImporter(container, ctx.BuiltinTypes) 
            {
                IgnoreCompilerGeneratedField = true,
                IgnorePrivateMembers = false
            };
            ass.Importer = importer;

            var loader = new DynamicLoader(importer, ctx);
            ImportAppdomainAssemblies(a => importer.ImportAssembly(a, container.GlobalRootNamespace));

            loader.LoadReferences(container);
            ass.Create(AppDomain.CurrentDomain, AssemblyBuilderAccess.RunAndSave);
            container.CreateContainer();
            loader.LoadModules(ass, container.GlobalRootNamespace);
            container.InitializePredefinedTypes();
            container.Define();

            if (ctx.Report.Errors > 0)
            {
                logger?.WriteLine("Found errors! Aborting compilation...");
                return null;
            }

            try
            {
                ass.Resolve();
                ass.Emit();
                container.CloseContainer();
                ass.EmbedResources();
            }
            catch (Exception e)
            {
                logger?.WriteLine($"Failed to compile because {e}");
                return null;
            }

            return ass.Builder;
        }

        private static AssemblyName ParseName(string fullName)
        {
            try
            {
                return new AssemblyName(fullName);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static void ImportAppdomainAssemblies(Action<Assembly> import)
        {
            // In some cases there could be multiple versions of the same assembly loaded
            // In that case we decide to simply load only the latest one as it's easiest to handle
            var dedupedAssemblies = AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => new {ass = a, name = ParseName(a.FullName)})
                .Where(a => a.name != null)
                .GroupBy(a => a.name.Name)
                .Select(g => g.OrderByDescending(a => a.name.Version).First());
            foreach (var ass in dedupedAssemblies)
            {
                if (StdLib.Contains(ass.name.Name) || compiledAssemblies.Contains(ass.name.Name))
                    continue;
                import(ass.ass);
            }
        }

        private static CompilerContext CreateContext(ReportPrinter reportPrinter)
        {
            var settings = new CompilerSettings
            {
                Version = LanguageVersion.Experimental,
                GenerateDebugInfo = false,
                StdLib = true,
                Target = Target.Library
            };

            return new CompilerContext(settings, reportPrinter);
        }
    }
}