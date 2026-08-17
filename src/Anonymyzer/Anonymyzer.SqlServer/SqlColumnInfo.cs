namespace Anonymyzer.SqlServer;

using Anonymyzer.Base;

public class SqlColumnInfo : IColumnInfo
{
    public SqlColumnInfo(string columnName, DbDataType dataType)
    {
        Name = columnName;
        DataType = dataType;
    }

    public string Name { get; }

    public DbDataType DataType { get; }

    public int MaxLength { get; set; }

    public bool IsPartOfThePrimaryKey { get; set; } = false;

    public bool IsUnicodeText { get; set; } = false;

    public bool IsNullable { get; set; } = false;
}