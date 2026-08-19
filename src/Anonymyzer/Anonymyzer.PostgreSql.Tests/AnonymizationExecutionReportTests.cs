namespace Anonymyzer.PostgreSql.Tests;

using Anonymyzer.Base;
using Anonymyzer.Base.Generation;
using Anonymyzer.Console.Commands;
using Anonymyzer.Console.Planning;

public sealed class AnonymizationExecutionReportTests
{
    [Fact]
    public void WritesCompletedReportWithoutRowOrPrimaryKeyValues()
    {
        string directory = Path.Combine(Path.GetTempPath(), "Anonymyzer.Tests", Guid.NewGuid().ToString("N"));
        string configurationPath = Path.Combine(directory, "config.json");
        string reportPath = Path.Combine(directory, "report.json");
        Directory.CreateDirectory(directory);
        File.WriteAllText(configurationPath, "{\"database\":\"clone\"}");
        var table = new GeneratorTableReference("public", "people");
        var generator = new GeneratorDescriptor(
            "FixedText",
            "1.0.0",
            "Fixed text",
            GeneratorExecutionScope.Row,
            DbDataType.Text);
        var binding = new GeneratorBinding(
            table,
            new Dictionary<string, string> { ["Value"] = "Name" },
            new Dictionary<string, DbDataType> { ["Value"] = DbDataType.Text });
        var plan = new AnonymizationExecutionPlan(
            100,
            [new GeneratorExecutionPlanStep(
                "public.people/column:Name",
                table,
                generator,
                binding,
                new object(),
                Array.Empty<GeneratorDataRequirement>(),
                100)]);
        DateTimeOffset started = DateTimeOffset.Parse("2026-08-19T12:00:00Z");

        try
        {
            AnonymizationExecutionReport report = AnonymizationExecutionReport.Completed(
                started,
                started.AddSeconds(2),
                configurationPath,
                "PostgreSql",
                "detached_clone",
                Guid.Parse("11111111-2222-3333-4444-555555555555"),
                plan,
                new ExecutionWriteSliceAssessment(true, "ready", table, "Id"),
                new AnonymizationExecutionResult(3, 2, "SENSITIVE-PRIMARY-KEY"));

            AnonymizationExecutionReportWriter.Write(reportPath, report);

            string json = File.ReadAllText(reportPath);
            Assert.Contains("\"ProcessedRows\": 3", json);
            Assert.Contains("\"CommittedBatches\": 2", json);
            Assert.Contains("\"ConfigurationSha256\"", json);
            Assert.DoesNotContain("SENSITIVE-PRIMARY-KEY", json);
            Assert.False(File.Exists(reportPath + ".tmp"));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
