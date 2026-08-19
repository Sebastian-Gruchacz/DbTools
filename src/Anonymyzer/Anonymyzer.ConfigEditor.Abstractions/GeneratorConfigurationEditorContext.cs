namespace Anonymyzer.ConfigEditor.Abstractions;

public sealed record GeneratorConfigurationEditorContext(
    IReadOnlyList<GeneratorConfigurationTableOption> Tables)
{
    public static GeneratorConfigurationEditorContext Empty { get; } = new(
        Array.Empty<GeneratorConfigurationTableOption>());
}

public sealed record GeneratorConfigurationTableOption(
    string SchemaName,
    string TableName,
    IReadOnlyList<string> Columns)
{
    public IReadOnlyList<string> PrimaryKeyColumns { get; init; } = Array.Empty<string>();
}
