namespace Anonymyzer.ConfigEditor.Infrastructure;

using System.Data;
using Anonymyzer.Base;
using Anonymyzer.Base.Detection;
using Anonymyzer.Configuration;
using Anonymyzer.Configuration.Safety;
using Anonymyzer.Console.GenerateConfiguration;
using Anonymyzer.DatabaseAccess;
using Anonymyzer.LanguagePack.English;
using Anonymyzer.LanguagePack.Polish;
using Anonymyzer.PostgreSql;
using Anonymyzer.SqlServer;

internal sealed class DatabaseRescanService
{
    private readonly ColumnConfigurationBuilder _columnBuilder = new(
        new ColumnCandidateDetector(new IColumnCandidateRuleProvider[]
        {
            new EnglishColumnCandidateRuleProvider(),
            new PolishColumnCandidateRuleProvider()
        }));

    public Task<IReadOnlyList<TableProcessingOptions>> ScanAsync(
        AnonymizationConfiguration configuration,
        string connectionEnvironmentVariable,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionEnvironmentVariable);
        return Task.Run(
            () => Scan(configuration, connectionEnvironmentVariable, cancellationToken),
            cancellationToken);
    }

    private IReadOnlyList<TableProcessingOptions> Scan(
        AnonymizationConfiguration configuration,
        string connectionEnvironmentVariable,
        CancellationToken cancellationToken)
    {
        using IDbConnection connection = new ValidatedCloneConnectionFactory().Open(
            configuration,
            connectionEnvironmentVariable,
            cancellationToken);
        IAnonymyzerEngine engine = CreateEngine(configuration.Database.DatabaseEngine, connection);
        return engine.ListTables(listSystemTables: false)
            .Where(table => !DetachedCopySafetyValidator.IsMarkerTable(
                configuration.Database.DatabaseEngine,
                table.SchemaName,
                table.Name))
            .Select(table => _columnBuilder.CreateTable(engine, table))
            .Where(table => table.Columns.Count > 0)
            .ToArray();
    }

    private static IAnonymyzerEngine CreateEngine(string databaseEngine, IDbConnection connection)
    {
        if (databaseEngine.Equals("SqlServer", StringComparison.OrdinalIgnoreCase))
        {
            return new SqlServerAnonymyzerEngine(connection);
        }

        if (databaseEngine.Equals("PostgreSql", StringComparison.OrdinalIgnoreCase))
        {
            return new PostgreSqlAnonymyzerEngine(connection);
        }

        throw new InvalidOperationException($"Unsupported database engine '{databaseEngine}'.");
    }
}
