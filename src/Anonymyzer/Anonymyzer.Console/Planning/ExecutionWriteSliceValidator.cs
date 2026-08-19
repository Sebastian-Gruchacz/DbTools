namespace Anonymyzer.Console.Planning;

using Anonymyzer.Base.Generation;

internal sealed class ExecutionWriteSliceValidator
{
    public ExecutionWriteSliceAssessment Assess(
        AnonymizationExecutionPlan plan,
        ExecutionPlanDatabaseInspection inspection)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(inspection);
        if (plan.Steps.Count == 0)
        {
            return Unsupported("the plan contains no generator steps");
        }

        GeneratorTableReference[] targetTables = plan.Steps
            .Select(step => step.TargetTable)
            .DistinctBy(TableKey, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (targetTables.Length != 1)
        {
            return Unsupported("the first write slice supports exactly one target table");
        }

        GeneratorTableReference targetTable = targetTables[0];
        var readPrimaryKeys = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (GeneratorExecutionPlanStep step in plan.Steps)
        {
            foreach (GeneratorDataRequirement requirement in step.DataRequirements.Where(requirement =>
                         !TableKey(requirement.Table).Equals(TableKey(targetTable), StringComparison.OrdinalIgnoreCase)))
            {
                if (!requirement.RequiresCompleteScan || requirement.ValueSource != GeneratorValueSource.Original)
                {
                    return Unsupported(
                        $"external data requirement '{requirement.Alias}' must be a complete scan of original values");
                }

                if (!inspection.Steps[step.Id].DataRequirements.TryGetValue(
                        requirement.Alias,
                        out DataRequirementEstimate? estimate))
                {
                    return Unsupported($"external data requirement '{requirement.Alias}' was not inspected");
                }

                if (estimate.PrimaryKeyColumns.Count != 1)
                {
                    return Unsupported(
                        $"external data requirement '{requirement.Alias}' requires a table with a single-column primary key");
                }

                readPrimaryKeys[TableKey(requirement.Table)] = estimate.PrimaryKeyColumns[0];
            }
        }

        GeneratorDataRequirement? generatedCompleteScan = plan.Steps
            .SelectMany(step => step.DataRequirements)
            .FirstOrDefault(requirement =>
                requirement.RequiresCompleteScan
                && requirement.ValueSource == GeneratorValueSource.Generated);
        if (generatedCompleteScan is not null)
        {
            return Unsupported(
                $"complete-scan requirement '{generatedCompleteScan.Alias}' needs generated values");
        }

        string[] primaryKeyColumns = plan.Steps
            .Select(step => inspection.Steps[step.Id].PrimaryKeyColumns)
            .SelectMany(columns => columns)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (primaryKeyColumns.Length != 1)
        {
            return Unsupported(
                primaryKeyColumns.Length == 0
                    ? "the target table has no primary key"
                    : "the first write slice requires a single-column primary key");
        }

        string primaryKeyColumn = primaryKeyColumns[0];
        if (plan.Steps.SelectMany(step => step.Binding.Outputs.Values).Any(column =>
                column.Equals(primaryKeyColumn, StringComparison.OrdinalIgnoreCase)))
        {
            return Unsupported("the primary-key column is configured as a generator output");
        }

        return new ExecutionWriteSliceAssessment(
            true,
            readPrimaryKeys.Count == 0
                ? "ready for the single-table Row/Column write slice"
                : "ready for the single-target relational write slice",
            targetTable,
            primaryKeyColumn)
        {
            ReadPrimaryKeys = readPrimaryKeys
        };
    }

    private static ExecutionWriteSliceAssessment Unsupported(string reason) =>
        new(false, reason, null, null);

    private static string TableKey(GeneratorTableReference table) =>
        $"{table.SchemaName}\u001f{table.TableName}";
}

internal sealed record ExecutionWriteSliceAssessment(
    bool IsSupported,
    string Message,
    GeneratorTableReference? TargetTable,
    string? PrimaryKeyColumn)
{
    public IReadOnlyDictionary<string, string> ReadPrimaryKeys { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}
