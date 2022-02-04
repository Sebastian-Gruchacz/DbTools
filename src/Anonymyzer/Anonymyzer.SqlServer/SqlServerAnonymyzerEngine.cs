namespace Anonymyzer.SqlServer;

using System.Data;
using Anonymyzer.Base;

public class SqlServerAnonymyzerEngine : IAnonymyzerEngine
{
    private readonly IDbConnection _connection;

    public SqlServerAnonymyzerEngine(IDbConnection connection)
    {
        _connection = connection;
    }

    public IEnumerable<ITableInfo> ListTables(bool listSystemTables = false)
    {
        // TODO: extracting system tables needs master connection.... tricky... Maybe need two?

        var cmd = _connection.CreateCommand();
        cmd.CommandType = CommandType.Text;
        cmd.CommandText = @$"SELECT * FROM [{_connection.Database}].INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE'";

        var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            string tableName = reader[@"TABLE_NAME"] as string;
            string schemaName = reader[@"TABLE_SCHEMA"] as string;

            yield return new SqlTableInfo(tableName!, schemaName!);
        }

        reader.Close();
    }

    public IEnumerable<IColumnInfo> ListTextColumns(ITableInfo tableInfo)
    {
        IDbCommand cmd = _connection.CreateCommand();
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.CommandText = @"sp_columns";
        
        var parameter = cmd.CreateParameter();
        parameter.ParameterName = @"table_name";
        parameter.DbType = DbType.String;
        parameter.Value = tableInfo.Name;
        cmd.Parameters.Add(parameter);

        var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            string? columnName = reader[@"COLUMN_NAME"] as string;
            int? dataType = reader[@"DATA_TYPE"] as int?;
            string? dataTypeName = reader[@"TYPE_NAME"] as string;
            bool? nullable = reader[@"IS_NULLABLE"] as bool?;
            int? maxLength = reader[@"LENGTH"] as int?;

            if (dataTypeName!.Contains("char") || dataTypeName!.Contains("text"))
            {
                yield return new SqlColumnInfo(columnName!, DbDataType.Text)
                {
                    MaxLength = maxLength ?? 0,
                    Nullable = nullable ?? false
                };
            }
        }

        reader.Close();
    }
}