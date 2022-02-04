using Anonymyzer.Base;
using Anonymyzer.Console;
using Anonymyzer.Console.CommandLibraryElements;
using Anonymyzer.Console.GenerateConfiguration;
using Anonymyzer.Console.Implementation;
using Anonymyzer.SqlServer;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;

ServiceProvider serviceProvider = new ServiceCollection()
    .AddLogging(builder =>
    {
        builder.AddConsole();//.AddFilter(level => level >= LogLevel.Debug)
    })
    .AddTransient<IAnonymyzerEngineBuilder, SqlServerEngineBuilder>()
    .AddTransient<IDbConnectionBuilder, SqlServerConnectionBuilder>()
    .AddSingleton<ICommandLogger, CommandLogger>()
    .AddSingleton<IEngineFactory, EngineFactory>()
    .AddSingleton<IDbConnectionFactory, DbConnectionFactory>()
    .AddCommands()
    .BuildServiceProvider();


// generate
var config = new GenerateAnonymyzerConfigurationCommandParameters
{
    ConfigurationFilePath = @"J:\tmp\icecast.anonymyse.json",
    DoOverride = true,
    DatabaseEngine = @"SqlServer",
    ConnectionString = @"Data Source=DESKTOP-NTTF649;Initial Catalog = sqldb-icecast-test;Integrated Security=True;Connect Timeout=30;Encrypt=False;TrustServerCertificate=False;ApplicationIntent=ReadWrite;MultiSubnetFailover=False"
};
var cmd = (GenerateAnonymyzerConfigurationCommand)serviceProvider.GetService(typeof(GenerateAnonymyzerConfigurationCommand));
return cmd.Process(config);