namespace Anonymyzer.Console.Cli;

internal abstract record CliCommandOptions(string ConnectionEnvironmentVariable);

internal sealed record GenerateConfigCliOptions(
    string DatabaseEngine,
    string DatabaseName,
    string ConnectionEnvironmentVariable,
    Guid MarkerId,
    string OutputPath,
    bool Force) : CliCommandOptions(ConnectionEnvironmentVariable);

internal sealed record RunCliOptions(
    string ConnectionEnvironmentVariable,
    Guid MarkerId,
    string ConfigurationPath,
    bool DryRun,
    bool Execute) : CliCommandOptions(ConnectionEnvironmentVariable);

internal sealed record CliParseResult(CliCommandOptions? Command, string? Error, bool ShowHelp)
{
    public bool IsSuccess => Command is not null && Error is null && !ShowHelp;
}
