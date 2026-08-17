namespace Anonymyzer.PostgreSql.Tests;

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
