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

    public IEnumerable<IColumnInfo> ListColumns(ITableInfo tableInfo)
    {
        ArgumentNullException.ThrowIfNull(tableInfo);

        using var command = _connection.CreateCommand();
        command.CommandText = """
            SELECT
                column_info.column_id,
                column_info.name,
                COALESCE(TYPE_NAME(column_info.system_type_id), ''),
                column_info.max_length,
                column_info.is_nullable,
                CASE WHEN EXISTS (
                    SELECT 1
                    FROM sys.indexes AS index_info
                    JOIN sys.index_columns AS key_info
                      ON key_info.object_id = index_info.object_id
                     AND key_info.index_id = index_info.index_id
                    WHERE index_info.object_id = column_info.object_id
                      AND index_info.is_primary_key = 1
                      AND key_info.column_id = column_info.column_id
                ) THEN CAST(1 AS bit) ELSE CAST(0 AS bit) END AS is_primary_key
            FROM sys.columns AS column_info
            JOIN sys.tables AS table_info ON table_info.object_id = column_info.object_id
            JOIN sys.schemas AS schema_info ON schema_info.schema_id = table_info.schema_id
            WHERE schema_info.name = @schema_name
              AND table_info.name = @table_name
            ORDER BY column_info.column_id;
            """;
        command.Parameters.Add(new SqlParameter("schema_name", SqlDbType.NVarChar, 128) { Value = tableInfo.SchemaName });
        command.Parameters.Add(new SqlParameter("table_name", SqlDbType.NVarChar, 128) { Value = tableInfo.Name });

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            string dataTypeName = reader.GetString(2);
            int maxLength = reader.GetInt16(3);
            bool unicode = dataTypeName is "nchar" or "nvarchar" or "ntext";
            if (maxLength < 0 || dataTypeName is "text" or "ntext" or "image")
            {
                maxLength = 0;
            }

            if (unicode && maxLength > 0)
            {
                maxLength /= 2;
            }

            yield return new SqlColumnInfo(reader.GetInt32(0), reader.GetString(1), Classify(dataTypeName))
            {
                MaxLength = maxLength,
                IsNullable = reader.GetBoolean(4),
                IsPartOfThePrimaryKey = reader.GetBoolean(5),
                IsUnicodeText = unicode
            };
        }
    }

    private static DbDataType Classify(string typeName)
    {
        return typeName.ToLowerInvariant() switch
        {
            "char" or "nchar" or "varchar" or "nvarchar" or "text" or "ntext" or "sysname" => DbDataType.Text,
            "tinyint" or "smallint" or "int" or "bigint" => DbDataType.Integer,
            "decimal" or "numeric" or "money" or "smallmoney" or "float" or "real" => DbDataType.Decimal,
            "bit" => DbDataType.Boolean,
            "datetime" or "datetime2" or "datetimeoffset" or "smalldatetime" => DbDataType.DateTime,
            "date" => DbDataType.Date,
            "time" => DbDataType.Time,
            "uniqueidentifier" => DbDataType.Guid,
            "binary" or "varbinary" or "image" or "rowversion" or "timestamp" => DbDataType.Binary,
            "xml" => DbDataType.Xml,
            _ => DbDataType.Other
        };
    }
}
