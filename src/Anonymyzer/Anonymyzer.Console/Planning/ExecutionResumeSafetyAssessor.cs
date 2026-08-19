namespace Anonymyzer.Console.Planning;

using Anonymyzer.Base.Generation;

internal sealed class ExecutionResumeSafetyAssessor
{
    public ExecutionResumeSafetyAssessment Assess(AnonymizationExecutionPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        GeneratorExecutionPlanStep? nonRowStep = plan.Steps.FirstOrDefault(step =>
            step.Generator.Scope != GeneratorExecutionScope.Row);
        if (nonRowStep is not null)
        {
            return Unsafe($"step '{nonRowStep.Id}' has scope {nonRowStep.Generator.Scope}");
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

        GeneratorDataRequirement? completeScan = plan.Steps
            .SelectMany(step => step.DataRequirements)
            .FirstOrDefault(requirement => requirement.RequiresCompleteScan);
        if (completeScan is not null)
        {
            return Unsafe($"data requirement '{completeScan.Alias}' needs a complete scan");
        }

        var outputColumns = plan.Steps
            .SelectMany(step => step.Binding.Outputs.Values)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        GeneratorDataRequirement? overwrittenOriginalInput = plan.Steps
            .SelectMany(step => step.DataRequirements)
            .FirstOrDefault(requirement =>
                requirement.ValueSource == GeneratorValueSource.Original
                && requirement.Columns.Any(outputColumns.Contains));
        if (overwrittenOriginalInput is not null)
        {
            return Unsafe(
                $"original input '{overwrittenOriginalInput.Alias}' reads a column overwritten by the plan");
        }

        return new ExecutionResumeSafetyAssessment(
            true,
            "resume-safe: deterministic Row sessions can be replayed from the beginning");
    }

    private static ExecutionResumeSafetyAssessment Unsafe(string reason) =>
        new(false, $"resume is unsafe because {reason}");
}

internal sealed record ExecutionResumeSafetyAssessment(bool IsSupported, string Message);
