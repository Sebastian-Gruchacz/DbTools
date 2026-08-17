namespace Anonymyzer.Base;

public interface IAnonymyzerEngine
{
    IEnumerable<ITableInfo> ListTables(bool listSystemTables = false);

    IEnumerable<IColumnInfo> ListTextColumns(ITableInfo tableInfo);
}