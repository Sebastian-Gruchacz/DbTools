namespace Anonymyzer.SqlServer;

using Anonymyzer.Base;

public sealed class SqlColumnInfo : IColumnInfo
{
    public SqlColumnInfo(int ordinal, string columnName, DbDataType dataType)
    {
        Ordinal = ordinal;
        Name = columnName;
        DataType = dataType;
    }

    public int Ordinal { get; }

    public string Name { get; }

    public DbDataType DataType { get; }

    public int MaxLength { get; set; }

    public bool IsPartOfThePrimaryKey { get; set; } = false;

    public bool IsUnicodeText { get; set; } = false;

    public bool IsNullable { get; set; } = false;
}