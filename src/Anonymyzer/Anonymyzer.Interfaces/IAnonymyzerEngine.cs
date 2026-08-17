namespace Anonymyzer.Base;

public interface IAnonymyzerEngine
{
    IEnumerable<ITableInfo> ListTables(bool listSystemTables = false);

    IEnumerable<IColumnInfo> ListColumns(ITableInfo tableInfo);
}