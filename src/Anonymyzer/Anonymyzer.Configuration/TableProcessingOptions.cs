namespace Anonymyzer.Configuration;

using Newtonsoft.Json;

public sealed class TableProcessingOptions
{
    public string TableName { get; set; } = string.Empty;

    public string SchemaName { get; set; } = string.Empty;

    public bool Enabled { get; set; }

    public string SchemaStatus { get; set; } = "Current";

    public List<ColumnProcessingOptions> Columns { get; set; } = new();

    public List<string> PrimaryKeyColumns { get; set; } = new();

    public List<ForeignKeyConfiguration> ForeignKeys { get; set; } = new();

    public List<GenerationGroupConfiguration> GenerationGroups { get; set; } = new();

    [JsonIgnore]
    public bool HasCandidates => Columns.Any(column => column.Detection.IsCandidate);

    public static TableProcessingOptions DefaultForTable(string tableName, string schemaName)
    {
        return new TableProcessingOptions
        {
            TableName = tableName,
            SchemaName = schemaName
        };
    }
}
