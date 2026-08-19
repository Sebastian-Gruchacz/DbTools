namespace Anonymyzer.PostgreSql.Tests;

using Anonymyzer.Base;
using Anonymyzer.Base.Generation;
using Anonymyzer.Console.Commands;
using Anonymyzer.Console.Planning;

public sealed class AnonymizationExecutionCheckpointTests
{
    private const string CheckpointSecret = "test-checkpoint-secret-with-sufficient-entropy";

    [Fact]
    public void PersistsOnlyPrimaryKeyFingerprintAndReloadsProgress()
    {
        string directory = Path.Combine(Path.GetTempPath(), "Anonymyzer.Tests", Guid.NewGuid().ToString("N"));
        string configurationPath = Path.Combine(directory, "config.json");
        string checkpointPath = Path.Combine(directory, "checkpoint.json");
        Directory.CreateDirectory(directory);
        File.WriteAllText(configurationPath, "{\"database\":\"clone\"}");
        var table = new GeneratorTableReference("public", "people");
        var plan = new AnonymizationExecutionPlan(100, [CreateStep(table)]);

        try
        {
            AnonymizationExecutionCheckpoint checkpoint = AnonymizationExecutionCheckpoint.Create(
                configurationPath,
                "PostgreSql",
                "detached_clone",
                Guid.Parse("11111111-2222-3333-4444-555555555555"),
                plan,
                new ExecutionWriteSliceAssessment(true, "ready", table, "Id"));
            checkpoint.Advance(
                new AnonymizationExecutionProgress(100, 1, "SENSITIVE-KEY", 100),
                CheckpointSecret);

            AnonymizationExecutionCheckpointStore.Write(checkpointPath, checkpoint);

            string json = File.ReadAllText(checkpointPath);
            Assert.DoesNotContain("SENSITIVE-KEY", json);
            Assert.Contains(PrimaryKeyFingerprint.Compute("SENSITIVE-KEY", CheckpointSecret), json);
            Assert.DoesNotContain(CheckpointSecret, json);
            AnonymizationExecutionCheckpoint loaded = Assert.IsType<AnonymizationExecutionCheckpoint>(
                AnonymizationExecutionCheckpointStore.Load(checkpointPath));
            loaded.EnsureMatches(AnonymizationExecutionCheckpoint.Create(
                configurationPath,
                "PostgreSql",
                "detached_clone",
                Guid.Parse("11111111-2222-3333-4444-555555555555"),
                plan,
                new ExecutionWriteSliceAssessment(true, "ready", table, "Id")));
            Assert.Equal(100, loaded.ProcessedRows);
            Assert.Equal(1, loaded.CommittedBatches);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static GeneratorExecutionPlanStep CreateStep(GeneratorTableReference table)
    {
        var descriptor = new GeneratorDescriptor(
            "FixedText",
            "1.0.0",
            "Fixed text",
            GeneratorExecutionScope.Row,
            DbDataType.Text);
        return new GeneratorExecutionPlanStep(
            "public.people/value",
            table,
            descriptor,
            new GeneratorBinding(table, new Dictionary<string, string> { ["Value"] = "Name" }),
            new object(),
            Array.Empty<GeneratorDataRequirement>(),
            100);
    }
}
