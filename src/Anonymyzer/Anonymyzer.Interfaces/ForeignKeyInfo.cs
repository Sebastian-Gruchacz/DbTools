namespace Anonymyzer.Base;

public sealed record ForeignKeyInfo(
    string Name,
    IReadOnlyList<string> Columns,
    string ReferencedSchemaName,
    string ReferencedTableName,
    IReadOnlyList<string> ReferencedColumns);
