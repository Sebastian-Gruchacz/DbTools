namespace Anonymyzer.Console.Commands;

using System.Security.Cryptography;
using Anonymyzer.Base.Generation;
using Anonymyzer.Console.Planning;
using Newtonsoft.Json;

internal sealed class AnonymizationExecutionReport
{
    public int FormatVersion { get; init; } = 2;

    public string Status { get; init; } = "Completed";

    public DateTimeOffset StartedAtUtc { get; init; }

    public DateTimeOffset CompletedAtUtc { get; init; }

    public long DurationMilliseconds { get; init; }

    public string DatabaseEngine { get; init; } = string.Empty;

    public string DatabaseName { get; init; } = string.Empty;

    public Guid DetachedCopyMarkerId { get; init; }

    public string ConfigurationSha256 { get; init; } = string.Empty;

    public string SchemaName { get; init; } = string.Empty;

    public string TableName { get; init; } = string.Empty;

    public string PrimaryKeyColumn { get; init; } = string.Empty;

    public int BatchSize { get; init; }

    public long ProcessedRows { get; init; }

    public int CommittedBatches { get; init; }

    public AnonymizationExecutionValidationReport Validation { get; init; } = new();

    public IReadOnlyList<AnonymizationExecutionReportStep> Steps { get; init; } =
        Array.Empty<AnonymizationExecutionReportStep>();

    public static AnonymizationExecutionReport Completed(
        DateTimeOffset startedAtUtc,
        DateTimeOffset completedAtUtc,
        string configurationPath,
        string databaseEngine,
        string databaseName,
        Guid markerId,
        AnonymizationExecutionPlan plan,
        ExecutionWriteSliceAssessment writeSlice,
        AnonymizationExecutionResult result,
        PostExecutionValidationResult validation)
    {
        GeneratorTableReference table = writeSlice.TargetTable
            ?? throw new InvalidOperationException("A completed execution must have a target table.");
        return new AnonymizationExecutionReport
        {
            StartedAtUtc = startedAtUtc,
            CompletedAtUtc = completedAtUtc,
            DurationMilliseconds = Math.Max(0, (long)(completedAtUtc - startedAtUtc).TotalMilliseconds),
            DatabaseEngine = databaseEngine,
            DatabaseName = databaseName,
            DetachedCopyMarkerId = markerId,
            ConfigurationSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(configurationPath))),
            SchemaName = table.SchemaName,
            TableName = table.TableName,
            PrimaryKeyColumn = writeSlice.PrimaryKeyColumn!,
            BatchSize = plan.BatchSize,
            ProcessedRows = result.ProcessedRows,
            CommittedBatches = result.CommittedBatches,
            Status = validation.Passed ? "Completed" : "ValidationFailed",
            Validation = new AnonymizationExecutionValidationReport
            {
                Passed = validation.Passed,
                MarkerValid = validation.MarkerValid,
                SchemaValid = validation.SchemaValid,
                RowCountBefore = validation.RowCountBefore,
                RowCountAfter = validation.RowCountAfter,
                CheckedConstraints = validation.CheckedConstraints,
                Issues = validation.Issues
            },
            Steps = plan.Steps.Select(step => new AnonymizationExecutionReportStep
            {
                Id = step.Id,
                GeneratorType = step.Generator.Type,
                GeneratorVersion = step.Generator.Version,
                OutputColumns = step.Binding.Outputs.Values
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Order(StringComparer.OrdinalIgnoreCase)
                    .ToArray()
            }).ToArray()
        };
    }
}

internal sealed class AnonymizationExecutionValidationReport
{
    public bool Passed { get; init; }

    public bool MarkerValid { get; init; }

    public bool SchemaValid { get; init; }

    public long RowCountBefore { get; init; }

    public long? RowCountAfter { get; init; }

    public int CheckedConstraints { get; init; }

    public IReadOnlyList<string> Issues { get; init; } = Array.Empty<string>();
}

internal sealed class AnonymizationExecutionReportStep
{
    public string Id { get; init; } = string.Empty;

    public string GeneratorType { get; init; } = string.Empty;

    public string GeneratorVersion { get; init; } = string.Empty;

    public IReadOnlyList<string> OutputColumns { get; init; } = Array.Empty<string>();
}

internal static class AnonymizationExecutionReportWriter
{
    public static void Write(string path, AnonymizationExecutionReport report)
    {
        string fullPath = Path.GetFullPath(path);
        string? directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        string temporaryPath = fullPath + ".tmp";
        string json = JsonConvert.SerializeObject(report, Formatting.Indented) + Environment.NewLine;
        File.WriteAllText(temporaryPath, json);
        File.Move(temporaryPath, fullPath, overwrite: true);
    }
}
