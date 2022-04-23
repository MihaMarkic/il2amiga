using System.Collections.Immutable;
using System.Runtime.Loader;
using System.Text;
using IL2Amiga.Engine;
using Microsoft.Extensions.DependencyInjection;

using var serviceProvider = new ServiceCollection()
            .AddLogging()
            .AddSingleton<Compiler>()
            .AddSingleton<PlugManager>()
            .AddSingleton<TypeResolver>()
            .AddSingleton<AppAssembler>()
            //.AddSingleton<VTablesImplRefs>()
            .AddSingleton<RuntimeEngineRefs>()
            .AddSingleton<ILScanner>()
            .AddSingleton<IsolatedAssemblyLoadContext>()
            .BuildServiceProvider();

string entryAssemblyFileName = args[0];

var compiler = serviceProvider.GetRequiredService<Compiler>();
var sb = new StringBuilder();
var writer = new StringWriter(sb);
compiler.Compile(entryAssemblyFileName, ImmutableArray<string>.Empty, writer);
Console.WriteLine(writer.ToString());