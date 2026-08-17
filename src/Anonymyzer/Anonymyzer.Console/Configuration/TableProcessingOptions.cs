namespace Anonymyzer.Console.Configuration;

internal class TableProcessingOptions
{
    public string TableName { get; set; } = string.Empty;
    
    public bool Enabled { get; set; }

    public List<ColumnProcessingOptions> Columns { get; set; } = new();

    public static TableProcessingOptions DefaultForTable(string tableName)
    {
        return new TableProcessingOptions
        {
            TableName = tableName,
            Enabled = false,
            Columns = new List<ColumnProcessingOptions>()
        };
    }
}