namespace Anonymyzer.Console.Planning;

using System.Data;
using Anonymyzer.Base.Generation;

internal sealed class PostExecutionDatabaseValidator
{
    public long CountRows(
        IDbConnection connection,
        string databaseEngine,
        GeneratorTableReference table)
    {
        ArgumentNullException.ThrowIfNull(connection);
        using IDbCommand command = connection.CreateCommand();
        command.CommandType = CommandType.Text;
        command.CommandTimeout = 300;
        command.CommandText = databaseEngine.Equals("SqlServer", StringComparison.OrdinalIgnoreCase)
            ? $"SELECT COUNT_BIG(*) FROM {QualifiedTable(databaseEngine, table)};"
            : databaseEngine.Equals("PostgreSql", StringComparison.OrdinalIgnoreCase)
                ? $"SELECT COUNT(*) FROM {QualifiedTable(databaseEngine, table)};"
                : throw new InvalidOperationException($"Unsupported database engine '{databaseEngine}'.");
        object? value = command.ExecuteScalar();
        return value is null or DBNull
            ? throw new InvalidOperationException("Exact row-count validation returned NULL.")
            : Convert.ToInt64(value, System.Globalization.CultureInfo.InvariantCulture);
    }

    public ConstraintValidationResult ValidateConstraints(
        IDbConnection connection,
        string databaseEngine,
        GeneratorTableReference table)
    {
        ArgumentNullException.ThrowIfNull(connection);
        if (databaseEngine.Equals("SqlServer", StringComparison.OrdinalIgnoreCase))
        {
            return ValidateSqlServerConstraints(connection, table);
        }

        if (databaseEngine.Equals("PostgreSql", StringComparison.OrdinalIgnoreCase))
        {
            return ValidatePostgreSqlConstraints(connection, table);
        }

        throw new InvalidOperationException($"Unsupported database engine '{databaseEngine}'.");
    }

    private static ConstraintValidationResult ValidateSqlServerConstraints(
        IDbConnection connection,
        GeneratorTableReference table)
    {
        using IDbCommand command = connection.CreateCommand();
        command.CommandType = CommandType.Text;
        command.CommandTimeout = 300;
        int tableObjectId = GetSqlServerTableObjectId(connection, table);
        command.CommandText = $"DBCC CHECKCONSTRAINTS ({tableObjectId}) WITH ALL_CONSTRAINTS;";
        var issues = new List<string>();
        using (IDataReader reader = command.ExecuteReader())
        {
            do
            {
                while (reader.FieldCount > 1 && reader.Read())
                {
                    string constraintName = !reader.IsDBNull(1)
                        ? Convert.ToString(reader.GetValue(1), System.Globalization.CultureInfo.InvariantCulture) ?? "unknown"
                        : "unknown";
                    issues.Add($"Constraint '{constraintName}' has at least one violating row.");
                }
            }
            while (reader.NextResult());
        }

        int checkedConstraints = CountSqlServerConstraints(connection, table);
        return new ConstraintValidationResult(checkedConstraints, issues);
    }

    private static int GetSqlServerTableObjectId(IDbConnection connection, GeneratorTableReference table)
    {
        using IDbCommand command = connection.CreateCommand();
        command.CommandText = """
            SELECT table_info.object_id
            FROM sys.tables AS table_info
            JOIN sys.schemas AS schema_info ON schema_info.schema_id = table_info.schema_id
            WHERE schema_info.name = @schema_name AND table_info.name = @table_name;
            """;
        AddParameter(command, "schema_name", table.SchemaName);
        AddParameter(command, "table_name", table.TableName);
        object? value = command.ExecuteScalar();
        return value is null or DBNull
            ? throw new InvalidOperationException(
                $"Target table {table.SchemaName}.{table.TableName} no longer exists.")
            : Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture);
    }

    private static int CountSqlServerConstraints(IDbConnection connection, GeneratorTableReference table)
    {
        using IDbCommand command = connection.CreateCommand();
        command.CommandText = """
            SELECT COUNT(*)
            FROM (
                SELECT check_info.object_id
                FROM sys.check_constraints AS check_info
                JOIN sys.tables AS table_info ON table_info.object_id = check_info.parent_object_id
                JOIN sys.schemas AS schema_info ON schema_info.schema_id = table_info.schema_id
                WHERE schema_info.name = @schema_name AND table_info.name = @table_name
                UNION ALL
                SELECT foreign_key.object_id
                FROM sys.foreign_keys AS foreign_key
                JOIN sys.tables AS table_info ON table_info.object_id = foreign_key.parent_object_id
                JOIN sys.schemas AS schema_info ON schema_info.schema_id = table_info.schema_id
                WHERE schema_info.name = @schema_name AND table_info.name = @table_name
            ) AS constraints_to_check;
            """;
        AddParameter(command, "schema_name", table.SchemaName);
        AddParameter(command, "table_name", table.TableName);
        return Convert.ToInt32(command.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);
    }

    private static ConstraintValidationResult ValidatePostgreSqlConstraints(
        IDbConnection connection,
        GeneratorTableReference table)
    {
        using IDbTransaction transaction = connection.BeginTransaction(IsolationLevel.RepeatableRead);
        try
        {
            using (IDbCommand readOnly = connection.CreateCommand())
            {
                readOnly.Transaction = transaction;
                readOnly.CommandText = "SET TRANSACTION READ ONLY;";
                readOnly.ExecuteNonQuery();
            }

            var issues = new List<string>();
            int checkedConstraints = 0;
            foreach (PostgreSqlCheckConstraint constraint in ReadPostgreSqlCheckConstraints(
                         connection,
                         transaction,
                         table))
            {
                checkedConstraints++;
                string sql = $"SELECT 1 FROM {QualifiedTable("PostgreSql", table)} " +
                             $"WHERE NOT ({constraint.Expression}) LIMIT 1;";
                if (HasRow(connection, transaction, sql))
                {
                    issues.Add($"CHECK constraint '{constraint.Name}' has at least one violating row.");
                }
            }

            foreach (PostgreSqlForeignKey constraint in ReadPostgreSqlForeignKeys(
                         connection,
                         transaction,
                         table))
            {
                checkedConstraints++;
                if (HasRow(connection, transaction, BuildPostgreSqlForeignKeyViolationQuery(constraint)))
                {
                    issues.Add($"Foreign key '{constraint.Name}' has at least one violating row.");
                }
            }

            transaction.Commit();
            return new ConstraintValidationResult(checkedConstraints, issues);
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    private static IReadOnlyList<PostgreSqlCheckConstraint> ReadPostgreSqlCheckConstraints(
        IDbConnection connection,
        IDbTransaction transaction,
        GeneratorTableReference table)
    {
        using IDbCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT constraint_info.conname,
                   pg_catalog.pg_get_expr(constraint_info.conbin, constraint_info.conrelid)
            FROM pg_catalog.pg_constraint AS constraint_info
            JOIN pg_catalog.pg_class AS table_info ON table_info.oid = constraint_info.conrelid
            JOIN pg_catalog.pg_namespace AS schema_info ON schema_info.oid = table_info.relnamespace
            WHERE constraint_info.contype = 'c'
              AND schema_info.nspname = @schema_name
              AND table_info.relname = @table_name
            ORDER BY constraint_info.conname;
            """;
        AddParameter(command, "schema_name", table.SchemaName);
        AddParameter(command, "table_name", table.TableName);
        var constraints = new List<PostgreSqlCheckConstraint>();
        using IDataReader reader = command.ExecuteReader();
        while (reader.Read())
        {
            constraints.Add(new PostgreSqlCheckConstraint(reader.GetString(0), reader.GetString(1)));
        }

        return constraints;
    }

    private static IReadOnlyList<PostgreSqlForeignKey> ReadPostgreSqlForeignKeys(
        IDbConnection connection,
        IDbTransaction transaction,
        GeneratorTableReference table)
    {
        using IDbCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT constraint_info.conname,
                   parent_schema.nspname,
                   parent_table.relname,
                   constraint_info.confmatchtype,
                   ARRAY(
                       SELECT child_column.attname
                       FROM unnest(constraint_info.conkey) WITH ORDINALITY AS key_info(attnum, ordinal)
                       JOIN pg_catalog.pg_attribute AS child_column
                         ON child_column.attrelid = constraint_info.conrelid
                        AND child_column.attnum = key_info.attnum
                       ORDER BY key_info.ordinal),
                   ARRAY(
                       SELECT parent_column.attname
                       FROM unnest(constraint_info.confkey) WITH ORDINALITY AS key_info(attnum, ordinal)
                       JOIN pg_catalog.pg_attribute AS parent_column
                         ON parent_column.attrelid = constraint_info.confrelid
                        AND parent_column.attnum = key_info.attnum
                       ORDER BY key_info.ordinal)
            FROM pg_catalog.pg_constraint AS constraint_info
            JOIN pg_catalog.pg_class AS child_table ON child_table.oid = constraint_info.conrelid
            JOIN pg_catalog.pg_namespace AS child_schema ON child_schema.oid = child_table.relnamespace
            JOIN pg_catalog.pg_class AS parent_table ON parent_table.oid = constraint_info.confrelid
            JOIN pg_catalog.pg_namespace AS parent_schema ON parent_schema.oid = parent_table.relnamespace
            WHERE constraint_info.contype = 'f'
              AND child_schema.nspname = @schema_name
              AND child_table.relname = @table_name
            ORDER BY constraint_info.conname;
            """;
        AddParameter(command, "schema_name", table.SchemaName);
        AddParameter(command, "table_name", table.TableName);
        var constraints = new List<PostgreSqlForeignKey>();
        using IDataReader reader = command.ExecuteReader();
        while (reader.Read())
        {
            constraints.Add(new PostgreSqlForeignKey(
                reader.GetString(0),
                table,
                new GeneratorTableReference(reader.GetString(1), reader.GetString(2)),
                Convert.ToChar(reader.GetValue(3), System.Globalization.CultureInfo.InvariantCulture) == 'f',
                ReadStringArray(reader.GetValue(4)),
                ReadStringArray(reader.GetValue(5))));
        }

        return constraints;
    }

    internal static string BuildPostgreSqlForeignKeyViolationQuery(PostgreSqlForeignKey constraint)
    {
        if (constraint.ChildColumns.Count == 0
            || constraint.ChildColumns.Count != constraint.ParentColumns.Count)
        {
            throw new InvalidOperationException($"Foreign key '{constraint.Name}' has invalid column metadata.");
        }

        string join = string.Join(" AND ", constraint.ChildColumns.Zip(
            constraint.ParentColumns,
            (child, parent) => $"child.{Quote("PostgreSql", child)} = parent.{Quote("PostgreSql", parent)}"));
        string[] childNullChecks = constraint.ChildColumns
            .Select(column => $"child.{Quote("PostgreSql", column)} IS NULL")
            .ToArray();
        string allNonNull = string.Join(" AND ", childNullChecks.Select(check => check.Replace(" IS NULL", " IS NOT NULL")));
        string childEligibility = constraint.MatchFull
            ? $"(({string.Join(" OR ", childNullChecks)}) AND ({string.Join(" OR ", childNullChecks.Select(check => check.Replace(" IS NULL", " IS NOT NULL"))) })) OR ({allNonNull})"
            : allNonNull;
        string missingParent = $"parent.{Quote("PostgreSql", constraint.ParentColumns[0])} IS NULL";
        return $"SELECT 1 FROM {QualifiedTable("PostgreSql", constraint.ChildTable)} AS child " +
               $"LEFT JOIN {QualifiedTable("PostgreSql", constraint.ParentTable)} AS parent ON {join} " +
               $"WHERE ({childEligibility}) AND {missingParent} LIMIT 1;";
    }

    private static bool HasRow(IDbConnection connection, IDbTransaction transaction, string sql)
    {
        using IDbCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandType = CommandType.Text;
        command.CommandTimeout = 300;
        command.CommandText = sql;
        using IDataReader reader = command.ExecuteReader();
        return reader.Read();
    }

    private static IReadOnlyList<string> ReadStringArray(object value) => value is Array array
        ? array.Cast<object>().Select(item => Convert.ToString(
            item,
            System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty).ToArray()
        : throw new InvalidOperationException("PostgreSQL returned invalid foreign-key column metadata.");

    private static string QualifiedTable(string databaseEngine, GeneratorTableReference table) =>
        $"{Quote(databaseEngine, table.SchemaName)}.{Quote(databaseEngine, table.TableName)}";

    private static string Quote(string databaseEngine, string identifier) =>
        databaseEngine.Equals("SqlServer", StringComparison.OrdinalIgnoreCase)
            ? $"[{identifier.Replace("]", "]]", StringComparison.Ordinal)}]"
            : databaseEngine.Equals("PostgreSql", StringComparison.OrdinalIgnoreCase)
                ? $"\"{identifier.Replace("\"", "\"\"", StringComparison.Ordinal)}\""
                : throw new InvalidOperationException($"Unsupported database engine '{databaseEngine}'.");

    private static void AddParameter(IDbCommand command, string name, object value)
    {
        IDbDataParameter parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}

internal sealed record ConstraintValidationResult(
    int CheckedConstraints,
    IReadOnlyList<string> Issues);

internal sealed record PostExecutionValidationResult(
    bool Passed,
    bool MarkerValid,
    bool SchemaValid,
    long RowCountBefore,
    long? RowCountAfter,
    int CheckedConstraints,
    IReadOnlyList<string> Issues);

internal sealed record PostgreSqlForeignKey(
    string Name,
    GeneratorTableReference ChildTable,
    GeneratorTableReference ParentTable,
    bool MatchFull,
    IReadOnlyList<string> ChildColumns,
    IReadOnlyList<string> ParentColumns);

internal sealed record PostgreSqlCheckConstraint(string Name, string Expression);
