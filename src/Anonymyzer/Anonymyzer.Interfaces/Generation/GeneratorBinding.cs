namespace Anonymyzer.Base.Generation;

public sealed class GeneratorBinding
{
    public GeneratorBinding(
        GeneratorTableReference table,
        IReadOnlyDictionary<string, string> outputs,
        IReadOnlyDictionary<string, DbDataType>? outputDataTypes = null)
    {
        Table = table ?? throw new ArgumentNullException(nameof(table));
        Outputs = outputs ?? throw new ArgumentNullException(nameof(outputs));
        OutputDataTypes = outputDataTypes ?? new Dictionary<string, DbDataType>(StringComparer.OrdinalIgnoreCase);
    }

    public GeneratorTableReference Table { get; }

    public IReadOnlyDictionary<string, string> Outputs { get; }

    public IReadOnlyDictionary<string, DbDataType> OutputDataTypes { get; }

    public string GetRequiredOutput(string outputName)
    {
        return Outputs.TryGetValue(outputName, out string? columnName)
            ? columnName
            : throw new InvalidOperationException($"Generator output '{outputName}' is not bound to a column.");
    }

    public bool TryGetOutput(string outputName, out string columnName)
    {
        return Outputs.TryGetValue(outputName, out columnName!);
    }

    public DbDataType GetOutputDataType(string outputName) =>
        OutputDataTypes.TryGetValue(outputName, out DbDataType dataType) ? dataType : DbDataType.Text;
}
