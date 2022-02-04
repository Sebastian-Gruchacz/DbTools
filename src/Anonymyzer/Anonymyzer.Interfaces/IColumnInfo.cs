namespace Anonymyzer.Base;

public interface IColumnInfo
{
    string Name { get; }

    DbDataType DataType { get; }

    bool Nullable { get; }

    int MaxLength { get; }

    bool IsUnicode { get; }
}