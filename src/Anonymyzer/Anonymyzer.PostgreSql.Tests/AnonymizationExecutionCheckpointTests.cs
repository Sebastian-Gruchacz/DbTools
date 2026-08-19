namespace Anonymyzer.PostgreSql.Tests;

using Anonymyzer.Base;
using Anonymyzer.Base.Generation;
using Anonymyzer.Console.Commands;
using Anonymyzer.Console.Planning;
using Newtonsoft.Json;

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
        var replayDependencies = new Dictionary<string, string>
        {
            ["public.people/value\u001fANONYMYZER_TEST_KEY"] =
                PrimaryKeyFingerprint.Compute("dependency-secret", CheckpointSecret)
        };

        try
        {
            AnonymizationExecutionCheckpoint checkpoint = AnonymizationExecutionCheckpoint.Create(
                configurationPath,
                "PostgreSql",
                "detached_clone",
                Guid.Parse("11111111-2222-3333-4444-555555555555"),
                plan,
                new ExecutionWriteSliceAssessment(true, "ready", table, "Id"),
                replayDependencies);
            checkpoint.Advance(
                new AnonymizationExecutionProgress(100, 1, "SENSITIVE-KEY", 100),
                CheckpointSecret);

            AnonymizationExecutionCheckpointStore.Write(checkpointPath, checkpoint);

            string json = File.ReadAllText(checkpointPath);
            Assert.DoesNotContain("SENSITIVE-KEY", json);
            Assert.Contains(PrimaryKeyFingerprint.Compute("SENSITIVE-KEY", CheckpointSecret), json);
            Assert.DoesNotContain(CheckpointSecret, json);
            Assert.DoesNotContain("dependency-secret", json);
            AnonymizationExecutionCheckpoint loaded = Assert.IsType<AnonymizationExecutionCheckpoint>(
                AnonymizationExecutionCheckpointStore.Load(checkpointPath));
            loaded.EnsureMatches(AnonymizationExecutionCheckpoint.Create(
                configurationPath,
                "PostgreSql",
                "detached_clone",
                Guid.Parse("11111111-2222-3333-4444-555555555555"),
                plan,
                new ExecutionWriteSliceAssessment(true, "ready", table, "Id"),
                replayDependencies));
            Assert.Equal(100, loaded.ProcessedRows);
            Assert.Equal(1, loaded.CommittedBatches);
            Assert.True(ReplayDependencyFingerprint.Matches(
                replayDependencies,
                loaded.ReplayDependencyHmacSha256));

            AnonymizationExecutionCheckpoint rowOnly = AnonymizationExecutionCheckpoint.Create(
                configurationPath,
                "PostgreSql",
                "detached_clone",
                Guid.Parse("11111111-2222-3333-4444-555555555555"),
                plan,
                new ExecutionWriteSliceAssessment(true, "ready", table, "Id"));
            string legacyJson = JsonConvert.SerializeObject(rowOnly)
                .Replace("\"FormatVersion\":2", "\"FormatVersion\":1", StringComparison.Ordinal);
            AnonymizationExecutionCheckpoint legacy = Assert.IsType<AnonymizationExecutionCheckpoint>(
                JsonConvert.DeserializeObject<AnonymizationExecutionCheckpoint>(legacyJson));
            legacy.EnsureMatches(rowOnly);

            string unsafeLegacyJson = JsonConvert.SerializeObject(checkpoint)
                .Replace("\"FormatVersion\":2", "\"FormatVersion\":1", StringComparison.Ordinal);
            AnonymizationExecutionCheckpoint unsafeLegacy = Assert.IsType<AnonymizationExecutionCheckpoint>(
                JsonConvert.DeserializeObject<AnonymizationExecutionCheckpoint>(unsafeLegacyJson));
            Assert.Throws<InvalidOperationException>(() => unsafeLegacy.EnsureMatches(checkpoint));
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
