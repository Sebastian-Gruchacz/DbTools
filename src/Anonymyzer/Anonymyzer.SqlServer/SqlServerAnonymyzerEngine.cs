namespace Anonymyzer.SqlServer;

using System.Data;
using Microsoft.Data.SqlClient;
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
        using var cmd = _connection.CreateCommand();
        cmd.CommandType = CommandType.Text;
        cmd.CommandText = """
            SELECT
                schema_info.name,
                table_info.name,
                COALESCE(SUM(CASE WHEN partition_info.index_id IN (0, 1) THEN partition_info.rows ELSE 0 END), 0)
            FROM sys.tables AS table_info
            JOIN sys.schemas AS schema_info ON schema_info.schema_id = table_info.schema_id
            LEFT JOIN sys.partitions AS partition_info ON partition_info.object_id = table_info.object_id
            WHERE @list_system = 1 OR table_info.is_ms_shipped = 0
            GROUP BY schema_info.name, table_info.name
            ORDER BY schema_info.name, table_info.name;
            """;
        cmd.Parameters.Add(new SqlParameter("list_system", SqlDbType.Bit) { Value = listSystemTables });

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            yield return new SqlTableInfo(reader.GetString(1), reader.GetString(0), reader.GetInt64(2));
        }
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
        schemaNameParameter.Value = tableInfo.SchemaName;
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
        schemaNameParameter.Value = tableInfo.SchemaName;
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
