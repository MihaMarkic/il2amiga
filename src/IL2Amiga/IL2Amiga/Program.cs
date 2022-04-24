using System.Collections.Immutable;
using System.Diagnostics;
using System.Text;
using IL2Amiga.Engine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;

using var serviceProvider = new ServiceCollection()
            .AddLogging(configure =>
            {
                configure.SetMinimumLevel(LogLevel.Debug);
                configure.AddConsole(options =>
                {
                    options.FormatterName = ConsoleFormatterNames.Systemd;
                });
            })
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
string plugsAssemblyFileName = args[1];

var compiler = serviceProvider.GetRequiredService<Compiler>();
var sb = new StringBuilder();
var writer = new StringWriter(sb);
compiler.Compile(entryAssemblyFileName, ImmutableArray<string>.Empty.Add(plugsAssemblyFileName), writer);
Console.WriteLine(writer.ToString());

if (Debugger.IsAttached)
{
    Console.ReadLine();
}