namespace Anonymyzer.Console.Planning;

using System.Globalization;
using Anonymyzer.Base.Generation;

internal static class ExecutionPlanFormatter
{
    public static IReadOnlyList<string> Format(
        AnonymizationExecutionPlan plan,
        ExecutionPlanDatabaseInspection? databaseInspection = null,
        ExecutionWriteSliceAssessment? writeSlice = null)
    {
        ArgumentNullException.ThrowIfNull(plan);
        var lines = new List<string>
        {
            $"Execution plan: {plan.Steps.Count} generator step(s), proposed batch size {plan.BatchSize}."
        };
        if (writeSlice is not null)
        {
            string status = writeSlice.IsSupported ? "ready" : "not ready";
            string target = writeSlice.IsSupported
                ? $" Target {writeSlice.TargetTable!.SchemaName}.{writeSlice.TargetTable.TableName}, " +
                  $"primary key {writeSlice.PrimaryKeyColumn}."
                : string.Empty;
            lines.Add($"Write slice {status}: {writeSlice.Message}.{target}");
        }

        for (int index = 0; index < plan.Steps.Count; index++)
        {
            GeneratorExecutionPlanStep step = plan.Steps[index];
            string outputs = string.Join(", ", step.Binding.Outputs.Select(binding => $"{binding.Key}->{binding.Value}"));
            lines.Add(
                $"  {index + 1}. {step.Id}: {step.Generator.Type} {step.Generator.Version} " +
                $"[{step.Generator.Scope}], outputs: {outputs}.");
            GeneratorStepDatabaseInspection? stepInspection = databaseInspection?.Steps.GetValueOrDefault(step.Id);
            if (stepInspection is not null)
            {
                lines.Add(
                    $"     estimated target rows: " +
                    $"{stepInspection.EstimatedTargetRows.ToString("N0", CultureInfo.InvariantCulture)}.");
            }

            foreach (GeneratorDataRequirement requirement in step.DataRequirements)
            {
                string scan = requirement.RequiresCompleteScan ? "complete scan" : "stream";
                DataRequirementEstimate? estimate = stepInspection?.DataRequirements.GetValueOrDefault(requirement.Alias);
                string estimateText = estimate is null
                    ? string.Empty
                    : $", estimated rows {estimate.EstimatedRows.ToString("N0", CultureInfo.InvariantCulture)}" +
                      (requirement.RequiresCompleteScan
                          ? $", rough max memory {FormatBytes(estimate.EstimatedMaximumMemoryBytes)}"
                          : string.Empty);
                lines.Add(
                    $"     data '{requirement.Alias}': {requirement.ValueSource} " +
                    $"{requirement.Table.SchemaName}.{requirement.Table.TableName}" +
                    $"({string.Join(", ", requirement.Columns)}), {scan}{estimateText}.");
            }
        }

        return lines;
    }

    private static string FormatBytes(long? bytes)
    {
        if (bytes is null)
        {
            return "unknown (unbounded text column)";
        }

        string[] units = ["B", "KiB", "MiB", "GiB", "TiB"];
        double value = bytes.Value;
        int unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return $"{value.ToString("0.##", CultureInfo.InvariantCulture)} {units[unit]}";
    }
}
