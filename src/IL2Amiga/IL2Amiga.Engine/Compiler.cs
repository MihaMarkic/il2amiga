using System.Collections.Immutable;
using System.Reflection;

namespace IL2Amiga.Engine
{
    public class Compiler
    {
        public void Compile(string entryAssemblyFileName, IEnumerable<string> assembliesFileNames, TextWriter writter)
        {
            var scanner = new ILScanner(null, null);
            var entryAssembler = Assembly.LoadFrom(entryAssemblyFileName);
            var programType = entryAssembler.GetTypes()
                .Where(t => string.Equals(t.Name, "Program", StringComparison.Ordinal))
                .SingleOrDefault()
                ?? throw new Exception("Couldn't find Program type");
            var entryMethod = programType.GetMethod("Main") ?? throw new Exception("Couldn't find Main method on Program");
            scanner.Execute(entryMethod, ImmutableArray<Assembly>.Empty);
        }
    }
}