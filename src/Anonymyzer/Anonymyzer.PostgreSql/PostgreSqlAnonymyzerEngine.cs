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
            SELECT
                namespace_info.nspname,
                table_info.relname,
                GREATEST(table_info.reltuples, 0)::bigint
            FROM pg_catalog.pg_class AS table_info
            JOIN pg_catalog.pg_namespace AS namespace_info
              ON namespace_info.oid = table_info.relnamespace
            WHERE table_info.relkind IN ('r', 'p')
              AND (@list_system OR namespace_info.nspname NOT IN ('pg_catalog', 'information_schema'))
            ORDER BY namespace_info.nspname, table_info.relname;
            """;
        command.Parameters.AddWithValue("list_system", listSystemTables);

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            yield return new PostgreSqlTableInfo(reader.GetString(1), reader.GetString(0), reader.GetInt64(2));
        }
    }

    public IEnumerable<IColumnInfo> ListColumns(ITableInfo tableInfo)
    {
        ArgumentNullException.ThrowIfNull(tableInfo);

        using var command = _connection.CreateCommand();
        command.CommandText = """
            SELECT
                column_info.ordinal_position,
                column_info.column_name,
                column_info.character_maximum_length,
                column_info.is_nullable,
                column_info.data_type,
                column_info.udt_name,
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
            ORDER BY column_info.ordinal_position;
            """;
        command.Parameters.AddWithValue("schema_name", tableInfo.SchemaName);
        command.Parameters.AddWithValue("table_name", tableInfo.Name);

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            string dataType = reader.GetString(4);
            string udtName = reader.GetString(5);
            DbDataType classifiedType = Classify(dataType, udtName);
            yield return new PostgreSqlColumnInfo(reader.GetInt32(0), reader.GetString(1), classifiedType)
            {
                MaxLength = reader.IsDBNull(2) ? 0 : reader.GetInt32(2),
                IsNullable = reader.GetString(3).Equals("YES", StringComparison.OrdinalIgnoreCase),
                IsPartOfThePrimaryKey = reader.GetBoolean(6),
                IsUnicodeText = classifiedType is DbDataType.Text or DbDataType.Json or DbDataType.Xml
            };
        }
    }

    public IEnumerable<ForeignKeyInfo> ListForeignKeys(ITableInfo tableInfo)
    {
        ArgumentNullException.ThrowIfNull(tableInfo);

        using var command = _connection.CreateCommand();
        command.CommandText = """
            SELECT
                constraint_info.conname,
                source_column.attname,
                referenced_namespace.nspname,
                referenced_table.relname,
                referenced_column.attname
            FROM pg_catalog.pg_constraint AS constraint_info
            JOIN pg_catalog.pg_class AS source_table
              ON source_table.oid = constraint_info.conrelid
            JOIN pg_catalog.pg_namespace AS source_namespace
              ON source_namespace.oid = source_table.relnamespace
            JOIN pg_catalog.pg_class AS referenced_table
              ON referenced_table.oid = constraint_info.confrelid
            JOIN pg_catalog.pg_namespace AS referenced_namespace
              ON referenced_namespace.oid = referenced_table.relnamespace
            JOIN LATERAL unnest(constraint_info.conkey, constraint_info.confkey)
              WITH ORDINALITY AS key_info(source_attnum, referenced_attnum, position) ON TRUE
            JOIN pg_catalog.pg_attribute AS source_column
              ON source_column.attrelid = source_table.oid
             AND source_column.attnum = key_info.source_attnum
            JOIN pg_catalog.pg_attribute AS referenced_column
              ON referenced_column.attrelid = referenced_table.oid
             AND referenced_column.attnum = key_info.referenced_attnum
            WHERE constraint_info.contype = 'f'
              AND source_namespace.nspname = @schema_name
              AND source_table.relname = @table_name
            ORDER BY constraint_info.conname, key_info.position;
            """;
        command.Parameters.AddWithValue("schema_name", tableInfo.SchemaName);
        command.Parameters.AddWithValue("table_name", tableInfo.Name);

        using var reader = command.ExecuteReader();
        return ReadForeignKeys(reader).ToArray();
    }

    private static IEnumerable<ForeignKeyInfo> ReadForeignKeys(NpgsqlDataReader reader)
    {
        string? currentName = null;
        string referencedSchema = string.Empty;
        string referencedTable = string.Empty;
        var columns = new List<string>();
        var referencedColumns = new List<string>();
        while (reader.Read())
        {
            string name = reader.GetString(0);
            if (currentName is not null && !name.Equals(currentName, StringComparison.Ordinal))
            {
                yield return new ForeignKeyInfo(
                    currentName,
                    columns.ToArray(),
                    referencedSchema,
                    referencedTable,
                    referencedColumns.ToArray());
                columns.Clear();
                referencedColumns.Clear();
            }

            currentName = name;
            columns.Add(reader.GetString(1));
            referencedSchema = reader.GetString(2);
            referencedTable = reader.GetString(3);
            referencedColumns.Add(reader.GetString(4));
        }

        if (currentName is not null)
        {
            yield return new ForeignKeyInfo(
                currentName,
                columns.ToArray(),
                referencedSchema,
                referencedTable,
                referencedColumns.ToArray());
        }
    }

    private static DbDataType Classify(string dataType, string udtName)
    {
        return dataType.ToLowerInvariant() switch
        {
            "character" or "character varying" or "text" => DbDataType.Text,
            "smallint" or "integer" or "bigint" => DbDataType.Integer,
            "numeric" or "decimal" or "real" or "double precision" => DbDataType.Decimal,
            "boolean" => DbDataType.Boolean,
            "timestamp without time zone" or "timestamp with time zone" => DbDataType.DateTime,
            "date" => DbDataType.Date,
            "time without time zone" or "time with time zone" or "interval" => DbDataType.Time,
            "uuid" => DbDataType.Guid,
            "bytea" => DbDataType.Binary,
            "json" or "jsonb" => DbDataType.Json,
            "xml" => DbDataType.Xml,
            "user-defined" when udtName.Equals("citext", StringComparison.OrdinalIgnoreCase) => DbDataType.Text,
            _ => DbDataType.Other
        };
    }
}

internal sealed record PostgreSqlTableInfo(string Name, string SchemaName, long EstimatedRowCount) : ITableInfo;

internal sealed class PostgreSqlColumnInfo(int ordinal, string name, DbDataType dataType) : IColumnInfo
{
    public int Ordinal { get; } = ordinal;
    public string Name { get; } = name;
    public DbDataType DataType { get; } = dataType;
    public bool IsNullable { get; init; }
    public bool IsPartOfThePrimaryKey { get; init; }
    public bool IsUnicodeText { get; init; }
    public int MaxLength { get; init; }
}
