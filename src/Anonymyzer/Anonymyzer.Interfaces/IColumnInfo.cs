namespace Anonymyzer.Base;

public interface IColumnInfo
{
    string Name { get; }

    DbDataType DataType { get; }

    bool IsNullable { get; }

    bool IsPartOfThePrimaryKey { get; }

    bool IsUnicodeText { get; }

    int MaxLength { get; }
}