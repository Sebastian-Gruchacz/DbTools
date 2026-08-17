namespace Anonymyzer.SqlServer;

using Anonymyzer.Base;

public class SqlTableInfo : ITableInfo
{
    public SqlTableInfo(string tableName, string schemaName, long estimatedRowCount)
    {
        Name = tableName;
        SchemaName = schemaName;
        EstimatedRowCount = estimatedRowCount;
    }

    public string SchemaName { get; }

    public string Name { get; }

    public long EstimatedRowCount { get; }
}
