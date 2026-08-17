namespace Anonymyzer.Console.Configuration;

internal class TableProcessingOptions
{
    public string TableName { get; set; } = string.Empty;

    public string SchemaName { get; set; } = string.Empty;

    public bool Enabled { get; set; }

    public List<ColumnProcessingOptions> Columns { get; set; } = new();

    public static TableProcessingOptions DefaultForTable(string tableName, string schemaName)
    {
        return new TableProcessingOptions
        {
            TableName = tableName,
            SchemaName = schemaName,
            Enabled = false,
            Columns = new List<ColumnProcessingOptions>()
        };
    }
}
