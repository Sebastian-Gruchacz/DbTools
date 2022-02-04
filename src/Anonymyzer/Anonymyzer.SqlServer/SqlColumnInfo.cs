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

    public bool IsUnicode { get; set; } = false;

    public bool Nullable { get; set; } = false;
}