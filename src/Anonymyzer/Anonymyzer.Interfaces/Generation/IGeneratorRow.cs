namespace Anonymyzer.Base.Generation;

public interface IGeneratorRow
{
    object? GetValue(string columnName);

    void SetValue(string columnName, object? value);
}
