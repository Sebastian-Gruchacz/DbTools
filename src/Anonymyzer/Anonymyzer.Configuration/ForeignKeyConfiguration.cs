namespace Anonymyzer.Configuration;

public sealed class ForeignKeyConfiguration
{
    public string Name { get; set; } = string.Empty;

    public List<string> Columns { get; set; } = new();

    public string ReferencedSchemaName { get; set; } = string.Empty;

    public string ReferencedTableName { get; set; } = string.Empty;

    public List<string> ReferencedColumns { get; set; } = new();
}
