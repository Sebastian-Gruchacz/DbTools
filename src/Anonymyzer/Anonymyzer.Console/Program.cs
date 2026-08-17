using Anonymyzer.Base;
using Anonymyzer.Console;
using Anonymyzer.Console.CommandLibraryElements;
using Anonymyzer.Console.Commands;
using Anonymyzer.Console.GenerateConfiguration;
using Anonymyzer.Console.Implementation;
using Anonymyzer.Console.InternalInterfaces;
using Anonymyzer.Generators.Simple;
using Anonymyzer.SqlServer;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;

ServiceProvider serviceProvider = new ServiceCollection()
    .AddLogging(builder =>
    {
        builder.AddConsole();//.AddFilter(level => level >= LogLevel.Debug)
    })
    .AddTransient<IAnonymyzerEngineBuilder, SqlServerEngineBuilder>() // TODO: improve init & connection abstraction
    .AddTransient<IDbConnectionBuilder, SqlServerConnectionBuilder>()
    .AddSingleton<ICommandLogger, CommandLogger>()
    .AddSingleton<IEngineFactory, EngineFactory>()
    .AddSingleton<IDbConnectionFactory, DbConnectionFactory>()
    .AddCommands()
    .AddGenerators(builder =>
    {
        builder.AddLoader<SimpleGeneratorsLoader>();
    })
    .BuildServiceProvider();


// generate
var config = new GenerateAnonymyzerConfigurationCommandParameters
{
    ConfigurationFilePath = @"J:\tmp\ows.anonymyse.json",
    DoOverride = true,
    DatabaseName = @"Test_OWS",
    DatabaseEngine = @"SqlServer",
    StructuralConnectionString = @"Data Source=DESKTOP-NTTF649;Integrated Security=True;Connect Timeout=30;Encrypt=False;TrustServerCertificate=False;ApplicationIntent=ReadWrite;MultiSubnetFailover=False",
    ConnectionString = @"Data Source=DESKTOP-NTTF649;Integrated Security=True;Connect Timeout=30;Encrypt=False;TrustServerCertificate=False;ApplicationIntent=ReadWrite;MultiSubnetFailover=False" 
    //Initial Catalog = Test_OWS
};
var cmd = (GenerateAnonymyzerConfigurationCommand)serviceProvider.GetService(typeof(GenerateAnonymyzerConfigurationCommand));
return cmd.Process(config);