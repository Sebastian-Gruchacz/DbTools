using Anonymyzer.Base;
using Anonymyzer.Console;
using Anonymyzer.Console.Cli;
using Anonymyzer.Console.CommandLibraryElements;
using Anonymyzer.Console.Commands;
using Anonymyzer.Console.GenerateConfiguration;
using Anonymyzer.Console.Implementation;
using Anonymyzer.Console.InternalInterfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

CliParseResult parsed = CliParser.Parse(args);
if (parsed.ShowHelp)
{
    global::System.Console.WriteLine(CliHelp.Text);
    return (int)ErrorCodes.Success;
}

if (!parsed.IsSuccess)
{
    global::System.Console.Error.WriteLine(parsed.Error);
    global::System.Console.Error.WriteLine("Use --help for usage.");
    return (int)ErrorCodes.ConfigurationError;
}

string? connectionString = Environment.GetEnvironmentVariable(parsed.Command!.ConnectionEnvironmentVariable);
if (string.IsNullOrWhiteSpace(connectionString))
{
    global::System.Console.Error.WriteLine(
        $"Environment variable '{parsed.Command.ConnectionEnvironmentVariable}' is empty or missing.");
    return (int)ErrorCodes.ConfigurationError;
}

var services = new ServiceCollection();
services.AddLogging(builder => builder.AddSimpleConsole(options => options.SingleLine = true));
services.AddSingleton<ICommandLogger, CommandLogger>();
services.AddTransient<IDbConnectionFactory, DbConnectionFactory>();
services.AddTransient<IEngineFactory, EngineFactory>();
services.AddDatabaseEngines();
services.AddBuiltInGenerators();
services.AddCommands();

using ServiceProvider provider = services.BuildServiceProvider();
try
{
    return parsed.Command switch
    {
        GenerateConfigCliOptions options => provider.GetRequiredService<GenerateAnonymyzerConfigurationCommand>()
            .Process(new GenerateAnonymyzerConfigurationCommandParameters
            {
                DatabaseEngine = options.DatabaseEngine,
                DatabaseName = options.DatabaseName,
                ConnectionString = connectionString,
                ExpectedMarkerId = options.MarkerId,
                ConfigurationFilePath = options.OutputPath,
                DoOverride = options.Force
            }),
        RunCliOptions options => provider.GetRequiredService<ProcessAnonymyzerCommand>()
            .Process(new ProcessAnonymyzerCommandParameters
            {
                ConnectionString = connectionString,
                ExpectedMarkerId = options.MarkerId,
                ConfigurationFilePath = options.ConfigurationPath,
                DryRun = options.DryRun
            }),
        _ => (int)ErrorCodes.ConfigurationError
    };
}
catch (Exception exception)
{
    global::System.Console.Error.WriteLine(exception.Message);
    return (int)ErrorCodes.ConfigurationError;
}
