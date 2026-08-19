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

    public IReadOnlyList<GeneratorConfigurationForeignKeyOption> ForeignKeys { get; init; } =
        Array.Empty<GeneratorConfigurationForeignKeyOption>();
}

public sealed record GeneratorConfigurationForeignKeyOption(
    string Name,
    IReadOnlyList<string> Columns,
    string ReferencedSchemaName,
    string ReferencedTableName,
    IReadOnlyList<string> ReferencedColumns);
