namespace Anonymyzer.Base;

public interface ITableInfo
{
    string Name { get; }

    string SchemaName { get; }

    long EstimatedRowCount { get; }
}
