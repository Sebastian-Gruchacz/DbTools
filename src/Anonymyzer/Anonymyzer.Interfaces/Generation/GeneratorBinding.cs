namespace Anonymyzer.Base.Generation;

public sealed class GeneratorBinding
{
    public GeneratorBinding(
        GeneratorTableReference table,
        IReadOnlyDictionary<string, string> outputs)
    {
        Table = table ?? throw new ArgumentNullException(nameof(table));
        Outputs = outputs ?? throw new ArgumentNullException(nameof(outputs));
    }

    public GeneratorTableReference Table { get; }

    public IReadOnlyDictionary<string, string> Outputs { get; }

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
}
