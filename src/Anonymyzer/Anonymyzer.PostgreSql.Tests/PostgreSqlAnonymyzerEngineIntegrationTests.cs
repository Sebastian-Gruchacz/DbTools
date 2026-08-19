namespace Anonymyzer.PostgreSql.Tests;

using Anonymyzer.Base;
using Anonymyzer.Configuration.Safety;
using Npgsql;

public sealed class PostgreSqlAnonymyzerEngineIntegrationTests
{
    [Fact]
    public void ReadsDetachedCopyMarker()
    {
        string connectionString = RequireConnectionString();
        using var connection = new NpgsqlConnection(connectionString);
        connection.Open();

        DetachedCopyMarker marker = new DetachedCopyMarkerReader().Read("PostgreSql", connection);

        Assert.Equal(Guid.Parse("11111111-2222-3333-4444-555555555555"), marker.MarkerId);
        Assert.Equal("anonymyzer_test", marker.DatabaseName);
    }

    [Fact]
    public void ReadsTablesAndColumnMetadata()
    {
        string connectionString = RequireConnectionString();

        using var connection = new NpgsqlConnection(connectionString);
        connection.Open();
        var engine = new PostgreSqlAnonymyzerEngine(connection);

        var tables = engine.ListTables().ToArray();
        var customerTable = Assert.Single(tables, table => table is { SchemaName: "public", Name: "customer_data" });
        var labelTable = Assert.Single(tables, table => table is { SchemaName: "public", Name: "labels" });
        Assert.Contains(tables, table => table is { SchemaName: "audit", Name: "customer_data" });
        Assert.DoesNotContain(tables, table => table.SchemaName is "pg_catalog" or "information_schema");
        Assert.Equal(2, customerTable.EstimatedRowCount);
        Assert.Equal(1, labelTable.EstimatedRowCount);

        var customerColumns = engine.ListColumns(customerTable).ToArray();
        var id = Assert.Single(customerColumns, column => column.Name == "id");
        Assert.Equal(1, id.Ordinal);
        Assert.Equal(DbDataType.Integer, id.DataType);
        Assert.True(id.IsPartOfThePrimaryKey);

        var displayName = Assert.Single(customerColumns, column => column.Name == "display_name");
        Assert.Equal(2, displayName.Ordinal);
        Assert.Equal(DbDataType.Text, displayName.DataType);
        Assert.Equal(64, displayName.MaxLength);
        Assert.True(displayName.IsNullable);
        Assert.True(displayName.IsUnicodeText);
        Assert.False(displayName.IsPartOfThePrimaryKey);

        var notes = Assert.Single(customerColumns, column => column.Name == "notes");
        Assert.Equal(0, notes.MaxLength);

        var pesel = Assert.Single(customerColumns, column => column.Name == "pesel");
        Assert.Equal(DbDataType.Integer, pesel.DataType);

        var preferences = Assert.Single(customerColumns, column => column.Name == "preferences");
        Assert.Equal(DbDataType.Json, preferences.DataType);

        var labelColumns = engine.ListColumns(labelTable).ToArray();
        var code = Assert.Single(labelColumns, column => column.Name == "code");
        Assert.True(code.IsPartOfThePrimaryKey);
        Assert.False(code.IsNullable);
    }

    [Fact]
    public void ReadsCompositeForeignKeyMetadataInDeclaredOrder()
    {
        string connectionString = RequireConnectionString();
        using var connection = new NpgsqlConnection(connectionString);
        connection.Open();
        _ = new DetachedCopyMarkerReader().Read("PostgreSql", connection);
        string suffix = Guid.NewGuid().ToString("N")[..8];
        string lookupTable = $"fk_lookup_{suffix}";
        string targetTable = $"fk_target_{suffix}";
        string constraintName = $"fk_metadata_{suffix}";
        using var command = connection.CreateCommand();
        command.CommandText =
            $"CREATE TABLE public.\"{lookupTable}\" (\"TenantId\" integer, \"Id\" integer, " +
            $"PRIMARY KEY (\"TenantId\", \"Id\")); " +
            $"CREATE TABLE public.\"{targetTable}\" (\"LookupTenantId\" integer, \"LookupId\" integer, " +
            $"CONSTRAINT \"{constraintName}\" FOREIGN KEY (\"LookupTenantId\", \"LookupId\") " +
            $"REFERENCES public.\"{lookupTable}\"(\"TenantId\", \"Id\"));";
        command.ExecuteNonQuery();

        try
        {
            var engine = new PostgreSqlAnonymyzerEngine(connection);
            ITableInfo table = Assert.Single(engine.ListTables(), candidate =>
                candidate.SchemaName == "public" && candidate.Name == targetTable);

            ForeignKeyInfo foreignKey = Assert.Single(engine.ListForeignKeys(table));

            Assert.Equal(constraintName, foreignKey.Name);
            Assert.Equal(["LookupTenantId", "LookupId"], foreignKey.Columns);
            Assert.Equal("public", foreignKey.ReferencedSchemaName);
            Assert.Equal(lookupTable, foreignKey.ReferencedTableName);
            Assert.Equal(["TenantId", "Id"], foreignKey.ReferencedColumns);
        }
        finally
        {
            command.CommandText =
                $"DROP TABLE IF EXISTS public.\"{targetTable}\"; " +
                $"DROP TABLE IF EXISTS public.\"{lookupTable}\";";
            command.ExecuteNonQuery();
        }
    }

    private static string RequireConnectionString()
    {
        string? connectionString = Environment.GetEnvironmentVariable("ANONYMYZER_POSTGRES_CONNECTION");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            Assert.Skip("Set ANONYMYZER_POSTGRES_CONNECTION to run the PostgreSQL integration test.");
        }

        return connectionString;
    }
}
