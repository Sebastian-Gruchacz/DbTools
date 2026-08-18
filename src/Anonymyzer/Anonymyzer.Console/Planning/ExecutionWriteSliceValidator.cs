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
        GeneratorExecutionPlanStep? unsupportedScope = plan.Steps.FirstOrDefault(step =>
            step.Generator.Scope != GeneratorExecutionScope.Row);
        if (unsupportedScope is not null)
        {
            return Unsupported(
                $"step '{unsupportedScope.Id}' has unsupported scope {unsupportedScope.Generator.Scope}");
        }

        GeneratorDataRequirement? completeScan = plan.Steps
            .SelectMany(step => step.DataRequirements)
            .FirstOrDefault(requirement => requirement.RequiresCompleteScan);
        if (completeScan is not null)
        {
            return Unsupported($"data requirement '{completeScan.Alias}' requires a complete scan");
        }

        GeneratorDataRequirement? externalRequirement = plan.Steps
            .SelectMany(step => step.DataRequirements)
            .FirstOrDefault(requirement => !TableKey(requirement.Table).Equals(
                TableKey(targetTable),
                StringComparison.OrdinalIgnoreCase));
        if (externalRequirement is not null)
        {
            return Unsupported($"data requirement '{externalRequirement.Alias}' reads another table");
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
            "ready for the single-table Row write slice",
            targetTable,
            primaryKeyColumn);
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
    string? PrimaryKeyColumn);
