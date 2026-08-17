namespace Anonymyzer.Base.Generation;

public sealed class GeneratorDataRow
{
    private readonly IReadOnlyDictionary<string, object?> _values;

    public GeneratorDataRow(IReadOnlyDictionary<string, object?> values)
    {
        _values = values ?? throw new ArgumentNullException(nameof(values));
    }

    public object? GetValue(string columnName)
    {
        return _values.TryGetValue(columnName, out object? value)
            ? value
            : throw new KeyNotFoundException($"Column '{columnName}' was not loaded.");
    }
}
