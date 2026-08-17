namespace Anonymyzer.Base;

public interface IColumnInfo
{
    int Ordinal { get; }

    string Name { get; }

    DbDataType DataType { get; }

    bool IsNullable { get; }

    bool IsPartOfThePrimaryKey { get; }

    bool IsUnicodeText { get; }

    int MaxLength { get; }
}