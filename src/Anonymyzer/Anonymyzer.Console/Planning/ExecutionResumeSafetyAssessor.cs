namespace Anonymyzer.Console.Planning;

using Anonymyzer.Base.Generation;

internal sealed class ExecutionResumeSafetyAssessor
{
    public ExecutionResumeSafetyAssessment Assess(AnonymizationExecutionPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        GeneratorTableReference[] targetTables = plan.Steps
            .Select(step => step.TargetTable)
            .DistinctBy(TableKey, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (targetTables.Length != 1)
        {
            return Unsafe("resume requires exactly one target table");
        }

        GeneratorTableReference targetTable = targetTables[0];
        GeneratorExecutionPlanStep? unsupportedScope = plan.Steps.FirstOrDefault(step =>
            step.Generator.Scope is not GeneratorExecutionScope.Row and not GeneratorExecutionScope.Relational);
        if (unsupportedScope is not null)
        {
            return Unsafe($"step '{unsupportedScope.Id}' has scope {unsupportedScope.Generator.Scope}");
        }

        GeneratorExecutionPlanStep? nondeterministicStep = plan.Steps.FirstOrDefault(step =>
            !step.Generator.SupportsDeterministicReplay);
        if (nondeterministicStep is not null)
        {
            return Unsafe($"step '{nondeterministicStep.Id}' does not declare deterministic replay support");
        }

        GeneratorExecutionPlanStep? existingValueStep = plan.Steps.FirstOrDefault(step =>
            step.Generator.RequiresExistingValue);
        if (existingValueStep is not null)
        {
            return Unsafe($"step '{existingValueStep.Id}' depends on the value it overwrites");
        }

        GeneratorDataRequirement? unsafeCompleteScan = plan.Steps
            .SelectMany(step => step.DataRequirements)
            .FirstOrDefault(requirement =>
                requirement.RequiresCompleteScan
                && (requirement.ValueSource != GeneratorValueSource.Original
                    || TableKey(requirement.Table).Equals(TableKey(targetTable), StringComparison.OrdinalIgnoreCase)));
        if (unsafeCompleteScan is not null)
        {
            return Unsafe(
                $"data requirement '{unsafeCompleteScan.Alias}' needs a complete scan of mutable target data");
        }

        var outputColumns = plan.Steps
            .SelectMany(step => step.Binding.Outputs.Values)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        GeneratorDataRequirement? overwrittenOriginalInput = plan.Steps
            .SelectMany(step => step.DataRequirements)
            .FirstOrDefault(requirement =>
                requirement.ValueSource == GeneratorValueSource.Original
                && TableKey(requirement.Table).Equals(TableKey(targetTable), StringComparison.OrdinalIgnoreCase)
                && requirement.Columns.Any(outputColumns.Contains));
        if (overwrittenOriginalInput is not null)
        {
            return Unsafe(
                $"original input '{overwrittenOriginalInput.Alias}' reads a column overwritten by the plan");
        }

        return new ExecutionResumeSafetyAssessment(
            true,
            "resume-safe: deterministic Row and read-only Relational sessions can be replayed from the beginning");
    }

    private static ExecutionResumeSafetyAssessment Unsafe(string reason) =>
        new(false, $"resume is unsafe because {reason}");

    private static string TableKey(GeneratorTableReference table) =>
        $"{table.SchemaName}\u001f{table.TableName}";
}

internal sealed record ExecutionResumeSafetyAssessment(bool IsSupported, string Message);
