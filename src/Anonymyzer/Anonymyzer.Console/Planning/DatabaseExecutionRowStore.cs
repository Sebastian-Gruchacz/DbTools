namespace Anonymyzer.Console.Planning;

using System.Data;
using Anonymyzer.Base.Generation;

internal sealed class DatabaseExecutionRowStore(
    IDbConnection connection,
    string databaseEngine) : IExecutionRowStore
{
    public Task<IReadOnlyList<ExecutionSourceRow>> ReadNextBatchAsync(
        GeneratorTableReference table,
        string primaryKeyColumn,
        IReadOnlyList<string> columns,
        object? afterPrimaryKey,
        int batchSize,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string[] valueColumns = columns
            .Where(column => !column.Equals(primaryKeyColumn, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        using IDbCommand command = connection.CreateCommand();
        command.CommandType = CommandType.Text;
        command.CommandTimeout = 60;
        command.CommandText = BuildSelect(
            databaseEngine,
            table,
            primaryKeyColumn,
            valueColumns,
            afterPrimaryKey is not null);
        AddParameter(command, "take", batchSize, DbType.Int32);
        if (afterPrimaryKey is not null)
        {
            AddParameter(command, "after_key", afterPrimaryKey);
        }

        var rows = new List<ExecutionSourceRow>(batchSize);
        using IDataReader reader = command.ExecuteReader();
        while (reader.Read())
        {
            cancellationToken.ThrowIfCancellationRequested();
            object primaryKey = reader.GetValue(0);
            var values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                [primaryKeyColumn] = primaryKey
            };
            for (int index = 0; index < valueColumns.Length; index++)
            {
                values[valueColumns[index]] = reader.IsDBNull(index + 1) ? null : reader.GetValue(index + 1);
            }

            rows.Add(new ExecutionSourceRow(primaryKey, values));
        }

        return Task.FromResult<IReadOnlyList<ExecutionSourceRow>>(rows);
    }

    public Task WriteBatchAsync(
        GeneratorTableReference table,
        string primaryKeyColumn,
        IReadOnlyList<string> outputColumns,
        IReadOnlyList<ExecutionUpdatedRow> rows,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using IDbTransaction transaction = connection.BeginTransaction();
        try
        {
            foreach (ExecutionUpdatedRow row in rows)
            {
                cancellationToken.ThrowIfCancellationRequested();
                using IDbCommand command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandType = CommandType.Text;
                command.CommandTimeout = 60;
                command.CommandText = BuildUpdate(
                    databaseEngine,
                    table,
                    primaryKeyColumn,
                    outputColumns);
                for (int index = 0; index < outputColumns.Count; index++)
                {
                    AddParameter(command, $"value_{index}", row.Values[outputColumns[index]]);
                }

                AddParameter(command, "primary_key", row.PrimaryKey);
                int affectedRows = command.ExecuteNonQuery();
                if (affectedRows != 1)
                {
                    throw new InvalidOperationException(
                        $"Expected to update one row in {table.SchemaName}.{table.TableName}, " +
                        $"but the database reported {affectedRows}.");
                }
            }

            transaction.Commit();
            return Task.CompletedTask;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    internal static string BuildSelect(
        string databaseEngine,
        GeneratorTableReference table,
        string primaryKeyColumn,
        IReadOnlyList<string> valueColumns,
        bool hasAfterKey)
    {
        string primaryKey = Quote(databaseEngine, primaryKeyColumn);
        string selectedColumns = string.Join(", ", new[] { primaryKey }.Concat(
            valueColumns.Select(column => Quote(databaseEngine, column))));
        string afterKey = hasAfterKey ? $" WHERE {primaryKey} > @after_key" : string.Empty;
        string qualifiedTable = $"{Quote(databaseEngine, table.SchemaName)}.{Quote(databaseEngine, table.TableName)}";

        if (databaseEngine.Equals("SqlServer", StringComparison.OrdinalIgnoreCase))
        {
            return $"SELECT TOP (@take) {selectedColumns} FROM {qualifiedTable}" +
                   $"{afterKey} ORDER BY {primaryKey};";
        }

        if (databaseEngine.Equals("PostgreSql", StringComparison.OrdinalIgnoreCase))
        {
            return $"SELECT {selectedColumns} FROM {qualifiedTable}" +
                   $"{afterKey} ORDER BY {primaryKey} LIMIT @take;";
        }

        throw new InvalidOperationException($"Unsupported database engine '{databaseEngine}'.");
    }

    internal static string BuildUpdate(
        string databaseEngine,
        GeneratorTableReference table,
        string primaryKeyColumn,
        IReadOnlyList<string> outputColumns)
    {
        string assignments = string.Join(", ", outputColumns.Select((column, index) =>
            $"{Quote(databaseEngine, column)} = @value_{index}"));
        return $"UPDATE {Quote(databaseEngine, table.SchemaName)}.{Quote(databaseEngine, table.TableName)} " +
               $"SET {assignments} WHERE {Quote(databaseEngine, primaryKeyColumn)} = @primary_key;";
    }

    private static string Quote(string databaseEngine, string identifier)
    {
        if (databaseEngine.Equals("SqlServer", StringComparison.OrdinalIgnoreCase))
        {
            return $"[{identifier.Replace("]", "]]", StringComparison.Ordinal)}]";
        }

        if (databaseEngine.Equals("PostgreSql", StringComparison.OrdinalIgnoreCase))
        {
            return $"\"{identifier.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
        }

        throw new InvalidOperationException($"Unsupported database engine '{databaseEngine}'.");
    }

    private static void AddParameter(
        IDbCommand command,
        string name,
        object? value,
        DbType? dbType = null)
    {
        IDbDataParameter parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value ?? DBNull.Value;
        if (dbType is not null)
        {
            parameter.DbType = dbType.Value;
        }

        command.Parameters.Add(parameter);
    }
}
