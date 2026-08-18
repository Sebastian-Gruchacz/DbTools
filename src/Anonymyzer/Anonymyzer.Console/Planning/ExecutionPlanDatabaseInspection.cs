namespace Anonymyzer.Console.Planning;

internal sealed record ExecutionPlanDatabaseInspection(
    IReadOnlyDictionary<string, GeneratorStepDatabaseInspection> Steps);

internal sealed record GeneratorStepDatabaseInspection(
    long EstimatedTargetRows,
    IReadOnlyList<string> PrimaryKeyColumns,
    IReadOnlyDictionary<string, DataRequirementEstimate> DataRequirements);

internal sealed record DataRequirementEstimate(
    long EstimatedRows,
    long? EstimatedMaximumMemoryBytes);
