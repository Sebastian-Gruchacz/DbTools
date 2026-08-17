namespace Anonymyzer.PostgreSql.Tests;

using Anonymyzer.Configuration;
using Anonymyzer.Configuration.Safety;
using Anonymyzer.DatabaseAccess;
using Anonymyzer.SqlServer;
using Microsoft.Data.SqlClient;

public sealed class SqlServerAnonymyzerEngineIntegrationTests
{
    [Fact]
    public void ReadsTableRowEstimatesAndTextColumnMetadata()
    {
        string connectionString = RequireConnectionString();
        using var connection = new SqlConnection(connectionString);
        connection.Open();
        var engine = new SqlServerAnonymyzerEngine(connection);

        var tables = engine.ListTables().ToArray();

        Assert.NotEmpty(tables);
        Assert.All(tables, table => Assert.True(table.EstimatedRowCount >= 0));
        Assert.Contains(tables, table => table.EstimatedRowCount > 0);

        var markerTable = Assert.Single(tables, table =>
            table.SchemaName == "dbo" && table.Name == "__AnonymyzerDetachedCopy");
        Assert.Equal(1, markerTable.EstimatedRowCount);

        Assert.Contains(tables, table => engine.ListTextColumns(table).Any());
    }

    [Fact]
    public async Task ReadsAtMostRequestedSamplesFromValidatedClone()
    {
        string connectionString = RequireConnectionString();
        using var connection = new SqlConnection(connectionString);
        connection.Open();
        var engine = new SqlServerAnonymyzerEngine(connection);
        DetachedCopyMarker marker = new DetachedCopyMarkerReader().Read("SqlServer", connection);
        var selected = engine.ListTables().ToArray()
            .Where(table => table.Name != "__AnonymyzerDetachedCopy")
            .Select(table => new { Table = table, Column = engine.ListTextColumns(table).FirstOrDefault() })
            .First(item => item.Column is not null);
        var column = new ColumnProcessingOptions
        {
            Ordinal = 1,
            ColumnName = selected.Column!.Name,
            DataType = selected.Column.DataType.ToString(),
            MaxLength = selected.Column.MaxLength,
            Unicode = selected.Column.IsUnicodeText
        };
        var table = new TableProcessingOptions
        {
            SchemaName = selected.Table.SchemaName,
            TableName = selected.Table.Name,
            Columns = { column }
        };
        var configuration = new AnonymizationConfiguration
        {
            Database = new DatabaseTargetConfiguration
            {
                DatabaseEngine = "SqlServer",
                DatabaseName = connection.Database,
                DetachedCopyMarkerId = marker.MarkerId.ToString("D")
            },
            Tables = { table }
        };

        IReadOnlyList<ColumnSample> samples = await new ColumnSampleReader().ReadAsync(
            configuration,
            table,
            column,
            "ANONYMYZER_SQLSERVER_CONNECTION",
            maximumRows: 2,
            TestContext.Current.CancellationToken);

        Assert.InRange(samples.Count, 0, 2);
    }

    private static string RequireConnectionString()
    {
        string? connectionString = Environment.GetEnvironmentVariable("ANONYMYZER_SQLSERVER_CONNECTION");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            Assert.Skip("Set ANONYMYZER_SQLSERVER_CONNECTION to run the SQL Server integration test.");
        }

        return connectionString;
    }
}
