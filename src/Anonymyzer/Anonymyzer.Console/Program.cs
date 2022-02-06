using Anonymyzer.Base;
using Anonymyzer.Console;
using Anonymyzer.Console.CommandLibraryElements;
using Anonymyzer.Console.GenerateConfiguration;
using Anonymyzer.Console.Implementation;
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
    DatabaseEngine = @"SqlServer",
    ConnectionString = @"Data Source=DESKTOP-NTTF649;Initial Catalog = Test_OWS;Integrated Security=True;Connect Timeout=30;Encrypt=False;TrustServerCertificate=False;ApplicationIntent=ReadWrite;MultiSubnetFailover=False"
};
var cmd = (GenerateAnonymyzerConfigurationCommand)serviceProvider.GetService(typeof(GenerateAnonymyzerConfigurationCommand));
return cmd.Process(config);