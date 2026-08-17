namespace Anonymyzer.Console.Planning;

using Anonymyzer.Base.Generation;

internal static class ExecutionPlanFormatter
{
    public static IReadOnlyList<string> Format(AnonymizationExecutionPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        var lines = new List<string>
        {
            $"Execution plan: {plan.Steps.Count} generator step(s), proposed batch size {plan.BatchSize}."
        };

        for (int index = 0; index < plan.Steps.Count; index++)
        {
            GeneratorExecutionPlanStep step = plan.Steps[index];
            string outputs = string.Join(", ", step.Binding.Outputs.Select(binding => $"{binding.Key}->{binding.Value}"));
            lines.Add(
                $"  {index + 1}. {step.Id}: {step.Generator.Type} {step.Generator.Version} " +
                $"[{step.Generator.Scope}], outputs: {outputs}.");

            foreach (GeneratorDataRequirement requirement in step.DataRequirements)
            {
                string scan = requirement.RequiresCompleteScan ? "complete scan" : "stream";
                lines.Add(
                    $"     data '{requirement.Alias}': {requirement.ValueSource} " +
                    $"{requirement.Table.SchemaName}.{requirement.Table.TableName}" +
                    $"({string.Join(", ", requirement.Columns)}), {scan}.");
            }
        }

        return lines;
    }
}
