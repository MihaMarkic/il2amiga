using System.Collections.Immutable;
using System.Reflection;

namespace IL2Amiga.Engine
{
    public class Compiler
    {
        readonly ILScanner scanner;
        readonly IsolatedAssemblyLoadContext isolatedAssemblyLoadContext;
        public Compiler(ILScanner scanner, IsolatedAssemblyLoadContext isolatedAssemblyLoadContext)
        {
            this.scanner = scanner;
            this.isolatedAssemblyLoadContext = isolatedAssemblyLoadContext;
        }
        public void Compile(string entryAssemblyFileName, IEnumerable<string> assembliesFileNames, TextWriter writer)
        {
            // should be mSettings.References.Concat(mSettings.PlugsReferences).Append(mSettings.TargetAssembly));
            isolatedAssemblyLoadContext.Init(ImmutableArray<string>.Empty);
            var entryAssembler = Assembly.LoadFrom(entryAssemblyFileName);
            var programType = entryAssembler.GetTypes()
                .Where(t => string.Equals(t.Name, "Program", StringComparison.Ordinal))
                .SingleOrDefault()
                ?? throw new Exception("Couldn't find Program type");
            var entryMethod = programType.GetMethod("Main") ?? throw new Exception("Couldn't find Main method on Program");
            var assemblies = assembliesFileNames.Select(fn => Assembly.LoadFrom(fn)).ToImmutableArray();
            scanner.Execute(entryMethod, assemblies);
        }
    }
}