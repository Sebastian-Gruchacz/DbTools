namespace Anonymyzer.DatabaseAccess;

using System.Data;
using Anonymyzer.Base;
using Anonymyzer.Configuration;
using Anonymyzer.Configuration.Safety;
using Anonymyzer.PostgreSql;
using Anonymyzer.SqlServer;

public sealed class ColumnMetadataReader
{
    private readonly ValidatedCloneConnectionFactory _connectionFactory = new();
    private readonly IReadOnlyList<IAnonymyzerEngineBuilder> _engineBuilders = new IAnonymyzerEngineBuilder[]
    {
        new SqlServerEngineBuilder(),
        new PostgreSqlEngineBuilder()
    };

    public Task<IReadOnlyList<AvailableColumn>> ReadAvailableAsync(
        AnonymizationConfiguration configuration,
        TableProcessingOptions table,
        string connectionEnvironmentVariable,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(table);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionEnvironmentVariable);
        if (!configuration.Tables.Contains(table))
        {
            throw new InvalidOperationException("The selected table does not belong to the open configuration.");
        }

        if (DetachedCopySafetyValidator.IsMarkerTable(
                configuration.Database.DatabaseEngine,
                table.SchemaName,
                table.TableName))
        {
            throw new InvalidOperationException("Columns cannot be added from the detached-copy marker table.");
        }

        return Task.Run(
            () => ReadAvailable(configuration, table, connectionEnvironmentVariable, cancellationToken),
            cancellationToken);
    }

    private IReadOnlyList<AvailableColumn> ReadAvailable(
        AnonymizationConfiguration configuration,
        TableProcessingOptions table,
        string connectionEnvironmentVariable,
        CancellationToken cancellationToken)
    {
        using IDbConnection connection = _connectionFactory.Open(
            configuration,
            connectionEnvironmentVariable,
            cancellationToken);
        IAnonymyzerEngineBuilder engineBuilder = _engineBuilders.SingleOrDefault(candidate =>
            candidate.Name.Equals(configuration.Database.DatabaseEngine, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException(
                $"Unsupported database engine '{configuration.Database.DatabaseEngine}'.");
        IAnonymyzerEngine engine = engineBuilder.BuildEngine(connection);
        ITableInfo liveTable = engine.ListTables(listSystemTables: false).SingleOrDefault(candidate =>
            candidate.SchemaName.Equals(table.SchemaName, StringComparison.OrdinalIgnoreCase)
            && candidate.Name.Equals(table.TableName, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"Table {table.SchemaName}.{table.TableName} no longer exists.");
        cancellationToken.ThrowIfCancellationRequested();

        HashSet<string> configuredNames = table.Columns
            .Select(column => column.ColumnName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        return engine.ListColumns(liveTable)
            .Where(column => !column.IsPartOfThePrimaryKey && !configuredNames.Contains(column.Name))
            .Select(column => new AvailableColumn(
                column.Ordinal,
                column.Name,
                column.DataType.ToString(),
                column.MaxLength,
                column.IsUnicodeText))
            .ToArray();
    }
}

public sealed record AvailableColumn(
    int Ordinal,
    string ColumnName,
    string DataType,
    int MaxLength,
    bool Unicode);
