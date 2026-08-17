namespace Anonymyzer.Console.Planning;

using Anonymyzer.Base.Generation;

internal sealed record AnonymizationExecutionPlan(
    int BatchSize,
    IReadOnlyList<GeneratorExecutionPlanStep> Steps);

internal sealed record GeneratorExecutionPlanStep(
    string Id,
    GeneratorTableReference TargetTable,
    GeneratorDescriptor Generator,
    GeneratorBinding Binding,
    object Configuration,
    IReadOnlyList<GeneratorDataRequirement> DataRequirements,
    int BatchSize);
