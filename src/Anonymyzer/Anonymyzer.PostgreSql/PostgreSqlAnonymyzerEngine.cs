namespace Anonymyzer.PostgreSql;

using System.Data;
using Anonymyzer.Base;
using Npgsql;

public sealed class PostgreSqlAnonymyzerEngine : IAnonymyzerEngine
{
    private readonly NpgsqlConnection _connection;

    public PostgreSqlAnonymyzerEngine(IDbConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);
        _connection = connection as NpgsqlConnection
            ?? throw new ArgumentException($"Connection must be a {nameof(NpgsqlConnection)}.", nameof(connection));
    }

    public IEnumerable<ITableInfo> ListTables(bool listSystemTables = false)
    {
        using var command = _connection.CreateCommand();
        command.CommandText = """
            SELECT table_schema, table_name
            FROM information_schema.tables
            WHERE table_type = 'BASE TABLE'
              AND (@list_system OR table_schema NOT IN ('pg_catalog', 'information_schema'))
            ORDER BY table_schema, table_name;
            """;
        command.Parameters.AddWithValue("list_system", listSystemTables);

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            yield return new PostgreSqlTableInfo(reader.GetString(1), reader.GetString(0));
        }
    }

    public IEnumerable<IColumnInfo> ListTextColumns(ITableInfo tableInfo)
    {
        ArgumentNullException.ThrowIfNull(tableInfo);

        using var command = _connection.CreateCommand();
        command.CommandText = """
            SELECT
                column_info.column_name,
                column_info.character_maximum_length,
                column_info.is_nullable,
                EXISTS (
                    SELECT 1
                    FROM information_schema.table_constraints AS constraint_info
                    JOIN information_schema.key_column_usage AS key_info
                      ON key_info.constraint_catalog = constraint_info.constraint_catalog
                     AND key_info.constraint_schema = constraint_info.constraint_schema
                     AND key_info.constraint_name = constraint_info.constraint_name
                    WHERE constraint_info.constraint_type = 'PRIMARY KEY'
                      AND constraint_info.table_schema = column_info.table_schema
                      AND constraint_info.table_name = column_info.table_name
                      AND key_info.column_name = column_info.column_name
                ) AS is_primary_key
            FROM information_schema.columns AS column_info
            WHERE column_info.table_schema = @schema_name
              AND column_info.table_name = @table_name
              AND column_info.data_type IN ('character', 'character varying', 'text')
            ORDER BY column_info.ordinal_position;
            """;
        command.Parameters.AddWithValue("schema_name", tableInfo.SchemaName);
        command.Parameters.AddWithValue("table_name", tableInfo.Name);

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            yield return new PostgreSqlColumnInfo(reader.GetString(0), DbDataType.Text)
            {
                MaxLength = reader.IsDBNull(1) ? 0 : reader.GetInt32(1),
                IsNullable = reader.GetString(2).Equals("YES", StringComparison.OrdinalIgnoreCase),
                IsPartOfThePrimaryKey = reader.GetBoolean(3),
                IsUnicodeText = true
            };
        }
    }
}

internal sealed record PostgreSqlTableInfo(string Name, string SchemaName) : ITableInfo;

internal sealed class PostgreSqlColumnInfo(string name, DbDataType dataType) : IColumnInfo
{
    public string Name { get; } = name;
    public DbDataType DataType { get; } = dataType;
    public bool IsNullable { get; init; }
    public bool IsPartOfThePrimaryKey { get; init; }
    public bool IsUnicodeText { get; init; }
    public int MaxLength { get; init; }
}
