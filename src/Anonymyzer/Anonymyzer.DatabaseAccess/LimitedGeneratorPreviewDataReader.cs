namespace Anonymyzer.DatabaseAccess;

using System.Data;
using System.Runtime.CompilerServices;
using Anonymyzer.Base.Generation;
using Anonymyzer.Configuration;
using Anonymyzer.Configuration.Safety;

public sealed class LimitedGeneratorPreviewDataReader : IGeneratorDataReader
{
    public const int MaximumPreviewRows = 50;

    private readonly AnonymizationConfiguration _configuration;
    private readonly string _connectionEnvironmentVariable;
    private readonly int _maximumRows;
    private readonly ValidatedCloneConnectionFactory _connectionFactory = new();

    public LimitedGeneratorPreviewDataReader(
        AnonymizationConfiguration configuration,
        string connectionEnvironmentVariable,
        int maximumRows)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionEnvironmentVariable);
        if (maximumRows is < 2 or > MaximumPreviewRows)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumRows),
                $"Preview size must be between 2 and {MaximumPreviewRows} rows.");
        }

        _connectionEnvironmentVariable = connectionEnvironmentVariable;
        _maximumRows = maximumRows;
    }

    public IReadOnlyList<GeneratorDataRow> LoadedRows { get; private set; } = Array.Empty<GeneratorDataRow>();

    public async IAsyncEnumerable<GeneratorDataRow> ReadAsync(
        GeneratorDataRequirement requirement,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requirement);
        GeneratorDataRow[] rows = await Task.Run(
            () => Read(requirement, cancellationToken),
            cancellationToken).ConfigureAwait(false);
        LoadedRows = rows;

        foreach (GeneratorDataRow row in rows)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return row;
        }
    }

    private GeneratorDataRow[] Read(
        GeneratorDataRequirement requirement,
        CancellationToken cancellationToken)
    {
        TableProcessingOptions table = ValidateRequirement(requirement);
        using IDbConnection connection = _connectionFactory.Open(
            _configuration,
            _connectionEnvironmentVariable,
            cancellationToken);
        using IDbCommand command = connection.CreateCommand();
        command.CommandTimeout = ColumnSampleReader.CommandTimeoutSeconds;
        command.CommandType = CommandType.Text;
        command.CommandText = BuildQuery(
            _configuration.Database.DatabaseEngine,
            table,
            requirement.Columns);
        IDbDataParameter takeParameter = command.CreateParameter();
        takeParameter.ParameterName = "take";
        takeParameter.DbType = DbType.Int32;
        takeParameter.Value = _maximumRows;
        command.Parameters.Add(takeParameter);
        IDbDataParameter maximumCharactersParameter = command.CreateParameter();
        maximumCharactersParameter.ParameterName = "max_characters";
        maximumCharactersParameter.DbType = DbType.Int32;
        maximumCharactersParameter.Value = ColumnSampleReader.MaximumCharactersPerValue;
        command.Parameters.Add(maximumCharactersParameter);

        var rows = new List<GeneratorDataRow>();
        using IDataReader reader = command.ExecuteReader();
        while (reader.Read())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            for (int index = 0; index < requirement.Columns.Count; index++)
            {
                values[requirement.Columns[index]] = reader.IsDBNull(index) ? null : reader.GetValue(index);
            }

            rows.Add(new GeneratorDataRow(values));
        }

        return rows.ToArray();
    }

    private TableProcessingOptions ValidateRequirement(GeneratorDataRequirement requirement)
    {
        TableProcessingOptions? table = _configuration.Tables.SingleOrDefault(candidate =>
            candidate.SchemaName.Equals(requirement.Table.SchemaName, StringComparison.OrdinalIgnoreCase)
            && candidate.TableName.Equals(requirement.Table.TableName, StringComparison.OrdinalIgnoreCase));
        if (table is null)
        {
            throw new InvalidOperationException("The preview requirement does not belong to the open configuration.");
        }

        if (DetachedCopySafetyValidator.IsMarkerTable(
                _configuration.Database.DatabaseEngine,
                table.SchemaName,
                table.TableName))
        {
            throw new InvalidOperationException("The detached-copy marker table cannot be previewed.");
        }

        foreach (string columnName in requirement.Columns)
        {
            if (!table.Columns.Any(column =>
                    column.ColumnName.Equals(columnName, StringComparison.OrdinalIgnoreCase))
                && !table.PrimaryKeyColumns.Contains(columnName, StringComparer.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Preview column '{columnName}' does not belong to the configured table.");
            }
        }

        return table;
    }

    private static string BuildQuery(
        string databaseEngine,
        TableProcessingOptions table,
        IReadOnlyList<string> columnNames)
    {
        if (columnNames.Count == 0)
        {
            throw new InvalidOperationException("The preview requirement contains no columns.");
        }

        if (databaseEngine.Equals("SqlServer", StringComparison.OrdinalIgnoreCase))
        {
            string columns = string.Join(", ", columnNames.Select(columnName =>
                $"LEFT(CAST({QuoteSqlServerIdentifier(columnName)} AS nvarchar(max)), @max_characters)"));
            return $"SELECT TOP (@take) {columns} FROM " +
                   $"{QuoteSqlServerIdentifier(table.SchemaName)}.{QuoteSqlServerIdentifier(table.TableName)};";
        }

        if (databaseEngine.Equals("PostgreSql", StringComparison.OrdinalIgnoreCase))
        {
            string columns = string.Join(", ", columnNames.Select(columnName =>
                $"LEFT({QuotePostgreSqlIdentifier(columnName)}::text, @max_characters)"));
            return $"SELECT {columns} FROM " +
                   $"{QuotePostgreSqlIdentifier(table.SchemaName)}.{QuotePostgreSqlIdentifier(table.TableName)} " +
                   "LIMIT @take;";
        }

        throw new InvalidOperationException($"Unsupported database engine '{databaseEngine}'.");
    }

    private static string QuoteSqlServerIdentifier(string identifier) =>
        $"[{identifier.Replace("]", "]]", StringComparison.Ordinal)}]";

    private static string QuotePostgreSqlIdentifier(string identifier) =>
        $"\"{identifier.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
}
