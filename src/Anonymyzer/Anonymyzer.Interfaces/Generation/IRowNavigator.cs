namespace Anonymyzer.Base.Generation;

public interface IRowNavigator
{
    public T GetValueOfCurrentColumn<T>();

    public void SaveValueToCurrentColumn<T>(T value);

    public T GetValueOfNamedColumn<T>(string columnName);
}