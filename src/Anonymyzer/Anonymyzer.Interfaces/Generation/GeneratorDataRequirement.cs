namespace Anonymyzer.Base.Generation;

public sealed record GeneratorDataRequirement(
    string Alias,
    GeneratorTableReference Table,
    IReadOnlyList<string> Columns,
    GeneratorValueSource ValueSource,
    bool RequiresCompleteScan);
