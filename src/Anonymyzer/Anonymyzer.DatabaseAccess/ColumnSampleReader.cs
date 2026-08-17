namespace Anonymyzer.DatabaseAccess;

using System.Data;
using System.Globalization;
using Anonymyzer.Base;
using Anonymyzer.Configuration;
using Anonymyzer.Configuration.Safety;
using Anonymyzer.PostgreSql;
using Anonymyzer.SqlServer;

public sealed class ColumnSampleReader
{
    public const int CommandTimeoutSeconds = 15;
    public const int MaximumCharactersPerValue = 32_768;

    private readonly IReadOnlyList<IDbConnectionBuilder> _connectionBuilders = new IDbConnectionBuilder[]
    {
        new SqlServerConnectionBuilder(),
        new PostgreSqlConnectionBuilder()
    };

    private readonly DetachedCopySafetyValidator _safetyValidator =
        new(new DetachedCopyMarkerReader());

    public Task<IReadOnlyList<ColumnSample>> ReadAsync(
        AnonymizationConfiguration configuration,
        TableProcessingOptions table,
        ColumnProcessingOptions column,
        string connectionEnvironmentVariable,
        int maximumRows,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(column);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionEnvironmentVariable);
        if (maximumRows is < 1 or > 50)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumRows), "Sample size must be between 1 and 50.");
        }

        return Task.Run(
            () => Read(
                configuration,
                table,
                column,
                connectionEnvironmentVariable,
                maximumRows,
                cancellationToken),
            cancellationToken);
    }

    private IReadOnlyList<ColumnSample> Read(
        AnonymizationConfiguration configuration,
        TableProcessingOptions table,
        ColumnProcessingOptions column,
        string connectionEnvironmentVariable,
        int maximumRows,
        CancellationToken cancellationToken)
    {
        ValidateSelection(configuration, table, column);
        string? connectionString = Environment.GetEnvironmentVariable(connectionEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Environment variable '{connectionEnvironmentVariable}' is empty or missing.");
        }

        IDbConnectionBuilder builder = _connectionBuilders.SingleOrDefault(candidate =>
            candidate.Name.Equals(configuration.Database.DatabaseEngine, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException(
                $"Unsupported database engine '{configuration.Database.DatabaseEngine}'.");

        using IDbConnection connection = builder.BuildMainConnection(
            connectionString,
            configuration.Database.DatabaseName);
        connection.Open();
        cancellationToken.ThrowIfCancellationRequested();

        if (!Guid.TryParse(configuration.Database.DetachedCopyMarkerId, out Guid markerId))
        {
            throw new InvalidOperationException("The configuration has no valid detached-copy marker id.");
        }

        _safetyValidator.Validate(configuration.Database, markerId, connection);

        using IDbCommand command = connection.CreateCommand();
        command.CommandTimeout = CommandTimeoutSeconds;
        command.CommandType = CommandType.Text;
        command.CommandText = BuildQuery(configuration.Database.DatabaseEngine, table, column);
        IDbDataParameter takeParameter = command.CreateParameter();
        takeParameter.ParameterName = "take";
        takeParameter.DbType = DbType.Int32;
        takeParameter.Value = maximumRows;
        command.Parameters.Add(takeParameter);
        IDbDataParameter maximumCharactersParameter = command.CreateParameter();
        maximumCharactersParameter.ParameterName = "max_characters";
        maximumCharactersParameter.DbType = DbType.Int32;
        maximumCharactersParameter.Value = MaximumCharactersPerValue;
        command.Parameters.Add(maximumCharactersParameter);

        var samples = new List<ColumnSample>();
        using IDataReader reader = command.ExecuteReader();
        while (reader.Read())
        {
            cancellationToken.ThrowIfCancellationRequested();
            string value = Convert.ToString(reader.GetValue(0), CultureInfo.InvariantCulture) ?? string.Empty;
            bool wasTruncated = Convert.ToBoolean(reader.GetValue(1), CultureInfo.InvariantCulture);
            samples.Add(new ColumnSample(samples.Count + 1, value, wasTruncated));
        }

        return samples;
    }

    private static void ValidateSelection(
        AnonymizationConfiguration configuration,
        TableProcessingOptions table,
        ColumnProcessingOptions column)
    {
        if (!configuration.Tables.Contains(table) || !table.Columns.Contains(column))
        {
            throw new InvalidOperationException("The selected column does not belong to the open configuration.");
        }

        if (DetachedCopySafetyValidator.IsMarkerTable(
                configuration.Database.DatabaseEngine,
                table.SchemaName,
                table.TableName))
        {
            throw new InvalidOperationException("The detached-copy marker table cannot be sampled.");
        }
    }

    private static string BuildQuery(
        string databaseEngine,
        TableProcessingOptions table,
        ColumnProcessingOptions column)
    {
        if (databaseEngine.Equals("SqlServer", StringComparison.OrdinalIgnoreCase))
        {
            string schema = QuoteSqlServerIdentifier(table.SchemaName);
            string tableName = QuoteSqlServerIdentifier(table.TableName);
            string columnName = QuoteSqlServerIdentifier(column.ColumnName);
            return $"SELECT TOP (@take) " +
                   $"LEFT(CAST({columnName} AS nvarchar(max)), @max_characters), " +
                   $"CASE WHEN DATALENGTH(CAST({columnName} AS nvarchar(max))) > @max_characters * 2 " +
                   $"THEN CAST(1 AS bit) ELSE CAST(0 AS bit) END " +
                   $"FROM {schema}.{tableName} WHERE {columnName} IS NOT NULL;";
        }

        if (databaseEngine.Equals("PostgreSql", StringComparison.OrdinalIgnoreCase))
        {
            string schema = QuotePostgreSqlIdentifier(table.SchemaName);
            string tableName = QuotePostgreSqlIdentifier(table.TableName);
            string columnName = QuotePostgreSqlIdentifier(column.ColumnName);
            return $"SELECT LEFT({columnName}::text, @max_characters), " +
                   $"char_length({columnName}::text) > @max_characters FROM {schema}.{tableName} " +
                   $"WHERE {columnName} IS NOT NULL LIMIT @take;";
        }

        throw new InvalidOperationException($"Unsupported database engine '{databaseEngine}'.");
    }

    private static string QuoteSqlServerIdentifier(string identifier) =>
        $"[{identifier.Replace("]", "]]", StringComparison.Ordinal)}]";

    private static string QuotePostgreSqlIdentifier(string identifier) =>
        $"\"{identifier.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
}

public sealed record ColumnSample(int Number, string Value, bool WasTruncated)
{
    public string DisplayValue =>
        (Value.Length == 0 ? "(empty string)" : Value) + (WasTruncated ? " … [truncated]" : string.Empty);
}
