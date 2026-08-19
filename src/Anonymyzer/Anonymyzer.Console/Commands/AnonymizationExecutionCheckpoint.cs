namespace Anonymyzer.Console.Commands;

using System.Security.Cryptography;
using Anonymyzer.Console.Planning;
using Newtonsoft.Json;

internal sealed class AnonymizationExecutionCheckpoint
{
    public int FormatVersion { get; init; } = 2;

    public string Status { get; set; } = "InProgress";

    public DateTimeOffset CreatedAtUtc { get; init; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public string DatabaseEngine { get; init; } = string.Empty;

    public string DatabaseName { get; init; } = string.Empty;

    public Guid DetachedCopyMarkerId { get; init; }

    public string ConfigurationSha256 { get; init; } = string.Empty;

    public string SchemaName { get; init; } = string.Empty;

    public string TableName { get; init; } = string.Empty;

    public string PrimaryKeyColumn { get; init; } = string.Empty;

    public int BatchSize { get; init; }

    public IReadOnlyDictionary<string, string> ReplayDependencyHmacSha256 { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);

    public long ProcessedRows { get; set; }

    public int CommittedBatches { get; set; }

    public string LastPrimaryKeyHmacSha256 { get; set; } = string.Empty;

    [JsonIgnore]
    public bool IsCompleted => string.Equals(Status, "Completed", StringComparison.OrdinalIgnoreCase);

    public static AnonymizationExecutionCheckpoint Create(
        string configurationPath,
        string databaseEngine,
        string databaseName,
        Guid markerId,
        AnonymizationExecutionPlan plan,
        ExecutionWriteSliceAssessment writeSlice,
        IReadOnlyDictionary<string, string>? replayDependencyFingerprints = null)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        return new AnonymizationExecutionCheckpoint
        {
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            DatabaseEngine = databaseEngine,
            DatabaseName = databaseName,
            DetachedCopyMarkerId = markerId,
            ConfigurationSha256 = ConfigurationFingerprint(configurationPath),
            SchemaName = writeSlice.TargetTable!.SchemaName,
            TableName = writeSlice.TargetTable.TableName,
            PrimaryKeyColumn = writeSlice.PrimaryKeyColumn!,
            BatchSize = plan.BatchSize,
            ReplayDependencyHmacSha256 = new Dictionary<string, string>(
                replayDependencyFingerprints ?? new Dictionary<string, string>(),
                StringComparer.Ordinal)
        };
    }

    public void EnsureMatches(AnonymizationExecutionCheckpoint expected)
    {
        if (!string.Equals(Status, "InProgress", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(Status, "Completed", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Checkpoint status is invalid.");
        }

        bool formatIsCompatible = FormatVersion == 2
            || (FormatVersion == 1
                && (ReplayDependencyHmacSha256?.Count ?? 0) == 0
                && expected.ReplayDependencyHmacSha256.Count == 0);
        if (!formatIsCompatible
            || !string.Equals(DatabaseEngine, expected.DatabaseEngine, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(DatabaseName, expected.DatabaseName, StringComparison.OrdinalIgnoreCase)
            || DetachedCopyMarkerId != expected.DetachedCopyMarkerId
            || !string.Equals(ConfigurationSha256, expected.ConfigurationSha256, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(SchemaName, expected.SchemaName, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(TableName, expected.TableName, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(PrimaryKeyColumn, expected.PrimaryKeyColumn, StringComparison.OrdinalIgnoreCase)
            || BatchSize != expected.BatchSize
            || ReplayDependencyHmacSha256 is null
            || !ReplayDependencyFingerprint.Matches(
                ReplayDependencyHmacSha256,
                expected.ReplayDependencyHmacSha256))
        {
            throw new InvalidOperationException(
                "Checkpoint does not match the validated clone, configuration, target table, primary key, " +
                "batch size, or replay dependencies.");
        }

        if (ProcessedRows < 0
            || CommittedBatches < 0
            || (ProcessedRows == 0 && (CommittedBatches != 0 || !string.IsNullOrEmpty(LastPrimaryKeyHmacSha256)))
            || (ProcessedRows > 0 && (CommittedBatches == 0 || LastPrimaryKeyHmacSha256?.Length != 64)))
        {
            throw new InvalidOperationException("Checkpoint progress counters are invalid.");
        }

        if (!string.IsNullOrEmpty(LastPrimaryKeyHmacSha256))
        {
            try
            {
                Convert.FromHexString(LastPrimaryKeyHmacSha256);
            }
            catch (FormatException exception)
            {
                throw new InvalidOperationException("Checkpoint primary-key fingerprint is invalid.", exception);
            }
        }

        foreach ((string dependency, string fingerprint) in ReplayDependencyHmacSha256)
        {
            if (string.IsNullOrWhiteSpace(dependency) || fingerprint?.Length != 64)
            {
                throw new InvalidOperationException("Checkpoint replay dependency fingerprint is invalid.");
            }

            try
            {
                Convert.FromHexString(fingerprint);
            }
            catch (FormatException exception)
            {
                throw new InvalidOperationException("Checkpoint replay dependency fingerprint is invalid.", exception);
            }
        }
    }

    public void Advance(AnonymizationExecutionProgress progress, string primaryKeyFingerprintSecret)
    {
        ProcessedRows = progress.ProcessedRows;
        CommittedBatches = progress.CommittedBatches;
        LastPrimaryKeyHmacSha256 = PrimaryKeyFingerprint.Compute(
            progress.LastPrimaryKey,
            primaryKeyFingerprintSecret);
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }

    public void Complete(AnonymizationExecutionResult result, string primaryKeyFingerprintSecret)
    {
        ProcessedRows = result.ProcessedRows;
        CommittedBatches = result.CommittedBatches;
        if (result.LastPrimaryKey is not null)
        {
            LastPrimaryKeyHmacSha256 = PrimaryKeyFingerprint.Compute(
                result.LastPrimaryKey,
                primaryKeyFingerprintSecret);
        }

        Status = "Completed";
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }

    private static string ConfigurationFingerprint(string path) =>
        Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.GetFullPath(path))));
}

internal static class AnonymizationExecutionCheckpointStore
{
    public static AnonymizationExecutionCheckpoint? Load(string path)
    {
        string fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath))
        {
            return null;
        }

        return JsonConvert.DeserializeObject<AnonymizationExecutionCheckpoint>(File.ReadAllText(fullPath))
            ?? throw new InvalidOperationException("Checkpoint file is empty.");
    }

    public static void Write(string path, AnonymizationExecutionCheckpoint checkpoint)
    {
        string fullPath = Path.GetFullPath(path);
        string? directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        string temporaryPath = fullPath + ".tmp";
        string json = JsonConvert.SerializeObject(checkpoint, Formatting.Indented) + Environment.NewLine;
        File.WriteAllText(temporaryPath, json);
        File.Move(temporaryPath, fullPath, overwrite: true);
    }
}
