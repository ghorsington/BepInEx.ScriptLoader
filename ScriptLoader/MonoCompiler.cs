using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using Mono.CSharp;

namespace ScriptLoader
{
    public interface ICSharpSource
    {
        byte[] Bytes { get; }
        string Location { get; }
        string Name { get; }
    }

    public class CSharpFile : ICSharpSource
    {
        public CSharpFile(string path)
        {
            if (!File.Exists(path))
                throw new FileNotFoundException("The given source file does not exist!", path);
            if (Path.GetExtension(path)?.ToLowerInvariant() != ".cs")
                throw new ArgumentException("The given path is not a .cs file!", nameof(path));
            Location = path;
        }

        public byte[] Bytes => File.ReadAllBytes(Location);
        public string Location { get; }

        public string Name => Path.GetFileName(Location);
    }

    public class CSharpCode : ICSharpSource
    {
        public CSharpCode()
        {
            SourceCode = new StringBuilder();
        }

        public StringBuilder SourceCode { get; }

        public byte[] Bytes => Encoding.UTF8.GetBytes(SourceCode.ToString());
        public string Location => "<eval>";
        public string Name => "<eval>";
    }

    public static class MonoCompiler
    {
        private static readonly HashSet<string> StdLib =
            new HashSet<string>(StringComparer.InvariantCultureIgnoreCase)
                {"mscorlib", "System.Core", "System", "System.Xml"};

        // Mimicked from https://github.com/kkdevs/Patchwork/blob/master/Patchwork/MonoScript.cs#L124
        public static Assembly Compile<T>(IList<T> sources, IEnumerable<Assembly> imports = null,
            TextWriter logger = null) where T : ICSharpSource
        {
            ReportPrinter reporter = logger == null ? new ConsoleReportPrinter() : new StreamReportPrinter(logger);
            Location.Reset();

            var dllName = $"compiled_{DateTime.Now.Ticks}";

            var ctx = CreateContext(reporter);
            ctx.Settings.SourceFiles.Clear();

            var i = 0;

            SeekableStreamReader GetFile(SourceFile file)
            {
                return new SeekableStreamReader(new MemoryStream(sources[file.Index].Bytes), Encoding.UTF8);
            }

            foreach (var source in sources)
            {
                ctx.Settings.SourceFiles.Add(new SourceFile(source.Name, source.Location, i, GetFile));
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

            var importer = new ReflectionImporter(container, ctx.BuiltinTypes);
            ass.Importer = importer;

            var loader = new DynamicLoader(importer, ctx);
            ImportAppdomainAssemblies(a => importer.ImportAssembly(a, container.GlobalRootNamespace));

            if (imports != null)
                foreach (var assembly in imports)
                    importer.ImportAssembly(assembly, container.GlobalRootNamespace);

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

        private static void ImportAppdomainAssemblies(Action<Assembly> import)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var name = assembly.GetName().Name;
                if (StdLib.Contains(name))
                    continue;
                import(assembly);
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