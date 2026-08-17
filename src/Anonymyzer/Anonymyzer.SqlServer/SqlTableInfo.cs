namespace Anonymyzer.SqlServer;

using Anonymyzer.Base;

public class SqlTableInfo : ITableInfo
{
    public SqlTableInfo(string tableName, string schemaName)
    {
        Name = tableName;
        SchemaName = schemaName;
    }

    public string SchemaName { get; }

    public string Name { get; }
}
