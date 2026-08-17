namespace Anonymyzer.PostgreSql.Tests;

using Npgsql;

public sealed class PostgreSqlAnonymyzerEngineIntegrationTests
{
    [Fact]
    public void ReadsTablesAndTextColumnMetadata()
    {
        var connectionString = Environment.GetEnvironmentVariable("ANONYMYZER_POSTGRES_CONNECTION");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            Assert.Skip("Set ANONYMYZER_POSTGRES_CONNECTION to run the PostgreSQL integration test.");
        }

        using var connection = new NpgsqlConnection(connectionString);
        connection.Open();
        var engine = new PostgreSqlAnonymyzerEngine(connection);

        var tables = engine.ListTables().ToArray();
        var customerTable = Assert.Single(tables, table => table is { SchemaName: "public", Name: "customer_data" });
        var labelTable = Assert.Single(tables, table => table is { SchemaName: "public", Name: "labels" });
        Assert.Contains(tables, table => table is { SchemaName: "audit", Name: "customer_data" });
        Assert.DoesNotContain(tables, table => table.SchemaName is "pg_catalog" or "information_schema");

        var customerColumns = engine.ListTextColumns(customerTable).ToArray();
        var displayName = Assert.Single(customerColumns, column => column.Name == "display_name");
        Assert.Equal(64, displayName.MaxLength);
        Assert.True(displayName.IsNullable);
        Assert.True(displayName.IsUnicodeText);
        Assert.False(displayName.IsPartOfThePrimaryKey);

        var notes = Assert.Single(customerColumns, column => column.Name == "notes");
        Assert.Equal(0, notes.MaxLength);

        var labelColumns = engine.ListTextColumns(labelTable).ToArray();
        var code = Assert.Single(labelColumns, column => column.Name == "code");
        Assert.True(code.IsPartOfThePrimaryKey);
        Assert.False(code.IsNullable);
    }
}
