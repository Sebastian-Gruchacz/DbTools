namespace Anonymyzer.SqlServer;

using System.Data;
using System.Data.SqlClient;
using Anonymyzer.Base;

public class SqlServerAnonymyzerEngine : IAnonymyzerEngine
{
    private readonly SqlConnection _connection;

    public SqlServerAnonymyzerEngine(IDbConnection connection)
    {
        _connection = (SqlConnection)connection ?? throw new ArgumentNullException(nameof(connection)); // MUST BE this type!
    }

    public IEnumerable<ITableInfo> ListTables(bool listSystemTables = false)
    {
        // TODO: extracting system tables needs master connection and different approach.... tricky...
        // TODO: Maybe need two diff connections - one for data and another one for the structure?

        using var cmd = _connection.CreateCommand();
        cmd.CommandType = CommandType.Text;
        cmd.CommandText = @$"SELECT * FROM [{_connection.Database}].INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE'";

        var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            string? tableName = reader[@"TABLE_NAME"] as string;
            string? schemaName = reader[@"TABLE_SCHEMA"] as string;

            yield return new SqlTableInfo(tableName!, schemaName!);
        }

        reader.Close();
    }

    public IEnumerable<IColumnInfo> ListTextColumns(ITableInfo tableInfo)
    {
        var primaryColumns = GetPrimaryKeyColumnNames(tableInfo).ToArray();

        using IDbCommand cmd = _connection.CreateCommand();
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.CommandText = @"sp_columns";
        
        var tableNameParameter = cmd.CreateParameter();
        tableNameParameter.ParameterName = @"table_name";
        tableNameParameter.DbType = DbType.String;
        tableNameParameter.Value = tableInfo.Name;
        cmd.Parameters.Add(tableNameParameter);

        var schemaNameParameter = cmd.CreateParameter();
        schemaNameParameter.ParameterName = @"table_owner"; // ?!?
        schemaNameParameter.DbType = DbType.String;
        schemaNameParameter.Value = ((SqlTableInfo)tableInfo).SchemaName;
        cmd.Parameters.Add(schemaNameParameter);

        var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            string? columnName = reader[@"COLUMN_NAME"] as string;
            int? dataType = reader[@"DATA_TYPE"] as int?;
            string? dataTypeName = reader[@"TYPE_NAME"] as string;
            bool? nullable = reader[@"IS_NULLABLE"] as bool?;
            int? length = reader[@"LENGTH"] as int?;
            int? precision = reader[@"PRECISION"] as int?;

            // TODO: this is very primitive, and text fields only, yet other types (xml and json especially) should also be supported
            if (dataTypeName!.Contains("char") || dataTypeName!.Contains("text"))
            {
                yield return new SqlColumnInfo(columnName!, DbDataType.Text)
                {
                    MaxLength = precision ?? 0,
                    IsNullable = nullable ?? false,
                    IsPartOfThePrimaryKey = primaryColumns.Contains(columnName),
                    IsUnicodeText = dataTypeName.StartsWith(@"n", StringComparison.InvariantCultureIgnoreCase)
                };
            }
        }

        reader.Close();
    }

    private IEnumerable<string> GetPrimaryKeyColumnNames(ITableInfo tableInfo)
    {
        using IDbCommand cmd = _connection.CreateCommand();
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.CommandText = @"sp_primary_keys_rowset";

        var parameter = cmd.CreateParameter();
        parameter.ParameterName = @"table_name";
        parameter.DbType = DbType.String;
        parameter.Value = tableInfo.Name;
        cmd.Parameters.Add(parameter);

        var schemaNameParameter = cmd.CreateParameter();
        schemaNameParameter.ParameterName = @"table_schema";
        schemaNameParameter.DbType = DbType.String;
        schemaNameParameter.Value = ((SqlTableInfo)tableInfo).SchemaName;
        cmd.Parameters.Add(schemaNameParameter);

        var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            string? columnName = reader[@"COLUMN_NAME"] as string;

            if (!string.IsNullOrWhiteSpace(columnName))
            {
                yield return columnName;
            }
        }

        reader.Close();
    }
}