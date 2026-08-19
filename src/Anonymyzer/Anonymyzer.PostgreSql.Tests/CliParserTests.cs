namespace Anonymyzer.PostgreSql.Tests;

using Anonymyzer.Console.Cli;

public sealed class CliParserTests
{
    private static readonly Guid MarkerId = Guid.Parse("11111111-2222-3333-4444-555555555555");

    [Fact]
    public void ParsesGenerateConfigWithoutAcceptingASecret()
    {
        CliParseResult result = CliParser.Parse(
        [
            "generate-config",
            "--engine", "PostgreSql",
            "--database", "anonymyzer_clone",
            "--connection-env", "ANONYMYZER_CONNECTION",
            "--marker-id", MarkerId.ToString("D"),
            "--output", "config.json",
            "--force"
        ]);

        var options = Assert.IsType<GenerateConfigCliOptions>(result.Command);
        Assert.True(result.IsSuccess);
        Assert.Equal(MarkerId, options.MarkerId);
        Assert.Equal("ANONYMYZER_CONNECTION", options.ConnectionEnvironmentVariable);
        Assert.True(options.Force);
    }

    [Fact]
    public void RejectsConnectionStringArgument()
    {
        CliParseResult result = CliParser.Parse(
        [
            "run",
            "--config", "config.json",
            "--connection-env", "ANONYMYZER_CONNECTION",
            "--marker-id", MarkerId.ToString("D"),
            "--dry-run",
            "--connection-string", "Host=production"
        ]);

        Assert.False(result.IsSuccess);
        Assert.Equal("Unknown option '--connection-string'.", result.Error);
    }

    [Fact]
    public void RejectsEmptyMarkerId()
    {
        CliParseResult result = CliParser.Parse(
        [
            "run",
            "--config", "config.json",
            "--connection-env", "ANONYMYZER_CONNECTION",
            "--marker-id", Guid.Empty.ToString("D"),
            "--dry-run"
        ]);

        Assert.False(result.IsSuccess);
        Assert.Contains("non-empty GUID", result.Error);
    }

    [Fact]
    public void RunRequiresExactlyOneExecutionMode()
    {
        CliParseResult result = CliParser.Parse(
        [
            "run",
            "--config", "config.json",
            "--connection-env", "ANONYMYZER_CONNECTION",
            "--marker-id", MarkerId.ToString("D")
        ]);

        Assert.False(result.IsSuccess);
        Assert.Contains("exactly one", result.Error);
    }

    [Fact]
    public void ParsesExplicitExecuteMode()
    {
        CliParseResult result = CliParser.Parse(
        [
            "run",
            "--config", "config.json",
            "--connection-env", "ANONYMYZER_CONNECTION",
            "--marker-id", MarkerId.ToString("D"),
            "--execute"
        ]);

        var options = Assert.IsType<RunCliOptions>(result.Command);
        Assert.True(options.Execute);
        Assert.False(options.DryRun);
        Assert.Null(options.ReportPath);
        Assert.Null(options.CheckpointPath);
    }

    [Fact]
    public void ParsesCheckpointPathForExecute()
    {
        CliParseResult result = CliParser.Parse(
        [
            "run",
            "--config", "config.json",
            "--connection-env", "ANONYMYZER_CONNECTION",
            "--marker-id", MarkerId.ToString("D"),
            "--execute",
            "--checkpoint", "execution.checkpoint.json",
            "--checkpoint-key-env", "ANONYMYZER_CHECKPOINT_KEY"
        ]);

        var options = Assert.IsType<RunCliOptions>(result.Command);
        Assert.Equal("execution.checkpoint.json", options.CheckpointPath);
        Assert.Equal("ANONYMYZER_CHECKPOINT_KEY", options.CheckpointKeyEnvironmentVariable);
    }

    [Fact]
    public void ParsesExecutionReportPath()
    {
        CliParseResult result = CliParser.Parse(
        [
            "run",
            "--config", "config.json",
            "--connection-env", "ANONYMYZER_CONNECTION",
            "--marker-id", MarkerId.ToString("D"),
            "--execute",
            "--report", "execution-report.json"
        ]);

        var options = Assert.IsType<RunCliOptions>(result.Command);
        Assert.Equal("execution-report.json", options.ReportPath);
    }

    [Fact]
    public void RejectsExecutionReportForDryRun()
    {
        CliParseResult result = CliParser.Parse(
        [
            "run",
            "--config", "config.json",
            "--connection-env", "ANONYMYZER_CONNECTION",
            "--marker-id", MarkerId.ToString("D"),
            "--dry-run",
            "--report", "execution-report.json"
        ]);

        Assert.False(result.IsSuccess);
        Assert.Contains("only with --execute", result.Error);
    }

    [Fact]
    public void RejectsDryRunAndExecuteTogether()
    {
        CliParseResult result = CliParser.Parse(
        [
            "run",
            "--config", "config.json",
            "--connection-env", "ANONYMYZER_CONNECTION",
            "--marker-id", MarkerId.ToString("D"),
            "--dry-run",
            "--execute"
        ]);

        Assert.False(result.IsSuccess);
        Assert.Contains("exactly one", result.Error);
    }
}
