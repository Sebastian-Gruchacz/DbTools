namespace Anonymyzer.Console.Planning;

using Anonymyzer.Base.Generation;

internal interface IExecutionRowStore
{
    Task<IReadOnlyList<ExecutionSourceRow>> ReadNextBatchAsync(
        GeneratorTableReference table,
        string primaryKeyColumn,
        IReadOnlyList<string> columns,
        object? afterPrimaryKey,
        int batchSize,
        CancellationToken cancellationToken);

    Task WriteBatchAsync(
        GeneratorTableReference table,
        string primaryKeyColumn,
        IReadOnlyList<ExecutionOutputColumn> outputColumns,
        IReadOnlyList<ExecutionUpdatedRow> rows,
        CancellationToken cancellationToken);
}

internal sealed record ExecutionSourceRow(
    object PrimaryKey,
    IReadOnlyDictionary<string, object?> Values);

internal sealed record ExecutionUpdatedRow(
    object PrimaryKey,
    IReadOnlyDictionary<string, object?> Values);

internal sealed record ExecutionOutputColumn(string Name, Anonymyzer.Base.DbDataType DataType);
