namespace Anonymyzer.Base.Generation;

public sealed record GeneratorDescriptor(
    string Type,
    string Version,
    string DisplayName,
    GeneratorExecutionScope Scope,
    DbDataType SupportedDataType)
{
    public IReadOnlyList<GeneratorOutputDescriptor> Outputs { get; init; } = Array.Empty<GeneratorOutputDescriptor>();

    public IReadOnlyList<DbDataType> SupportedDataTypes { get; init; } = [SupportedDataType];

    public bool RequiresExistingValue { get; init; }
}
