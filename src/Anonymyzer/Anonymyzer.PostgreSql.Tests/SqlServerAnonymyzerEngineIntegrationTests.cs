namespace Anonymyzer.PostgreSql.Tests;

using Anonymyzer.Base;
using Anonymyzer.Configuration;
using Anonymyzer.Configuration.Safety;
using Anonymyzer.DatabaseAccess;
using Anonymyzer.SqlServer;
using Microsoft.Data.SqlClient;

public sealed class SqlServerAnonymyzerEngineIntegrationTests
{
    [Fact]
    public void ReadsTableRowEstimatesAndColumnMetadata()
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

        Assert.Contains(tables, table => engine.ListColumns(table).Any());
        Assert.Contains(tables.SelectMany(engine.ListColumns), column => column.DataType == DbDataType.Integer);
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
            .Select(table => new
            {
                Table = table,
                Column = engine.ListColumns(table).FirstOrDefault(column => column.DataType == DbDataType.Text)
            })
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

    [Fact]
    public void ReadsCompositeForeignKeyMetadataInDeclaredOrder()
    {
        string connectionString = RequireConnectionString();
        using var connection = new SqlConnection(connectionString);
        connection.Open();
        _ = new DetachedCopyMarkerReader().Read("SqlServer", connection);
        string suffix = Guid.NewGuid().ToString("N")[..8];
        string lookupTable = $"fk_lookup_{suffix}";
        string targetTable = $"fk_target_{suffix}";
        string constraintName = $"fk_metadata_{suffix}";
        using var command = connection.CreateCommand();
        command.CommandText =
            $"CREATE TABLE [dbo].[{lookupTable}] ([TenantId] int, [Id] int, " +
            $"PRIMARY KEY ([TenantId], [Id])); " +
            $"CREATE TABLE [dbo].[{targetTable}] ([LookupTenantId] int, [LookupId] int, " +
            $"CONSTRAINT [{constraintName}] FOREIGN KEY ([LookupTenantId], [LookupId]) " +
            $"REFERENCES [dbo].[{lookupTable}]([TenantId], [Id]));";
        command.ExecuteNonQuery();

        try
        {
            var engine = new SqlServerAnonymyzerEngine(connection);
            ITableInfo table = Assert.Single(engine.ListTables(), candidate =>
                candidate.SchemaName == "dbo" && candidate.Name == targetTable);

            ForeignKeyInfo foreignKey = Assert.Single(engine.ListForeignKeys(table));

            Assert.Equal(constraintName, foreignKey.Name);
            Assert.Equal(["LookupTenantId", "LookupId"], foreignKey.Columns);
            Assert.Equal("dbo", foreignKey.ReferencedSchemaName);
            Assert.Equal(lookupTable, foreignKey.ReferencedTableName);
            Assert.Equal(["TenantId", "Id"], foreignKey.ReferencedColumns);
        }
        finally
        {
            command.CommandText =
                $"DROP TABLE IF EXISTS [dbo].[{targetTable}]; " +
                $"DROP TABLE IF EXISTS [dbo].[{lookupTable}];";
            command.ExecuteNonQuery();
        }
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
