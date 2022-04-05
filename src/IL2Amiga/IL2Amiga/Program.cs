using System.Collections.Immutable;
using System.Text;
using IL2Amiga.Engine;

string entryAssemblyFileName = args[0];

var compiler = new Compiler();
var sb = new StringBuilder();
var writer = new StringWriter(sb);
compiler.Compile(entryAssemblyFileName, ImmutableArray<string>.Empty, writer);
Console.WriteLine(writer.ToString());