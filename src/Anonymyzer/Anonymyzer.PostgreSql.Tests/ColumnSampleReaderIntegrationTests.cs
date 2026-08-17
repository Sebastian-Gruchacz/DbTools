namespace Anonymyzer.PostgreSql.Tests;

using Anonymyzer.Configuration;
using Anonymyzer.DatabaseAccess;

public sealed class ColumnSampleReaderIntegrationTests
{
    [Fact]
    public async Task ReadsOnlyNonNullValuesFromValidatedPostgreSqlClone()
    {
        string connectionString = RequireConnectionString();
        const string environmentVariable = "ANONYMYZER_SAMPLE_TEST_CONNECTION";
        string? previousValue = Environment.GetEnvironmentVariable(environmentVariable);
        Environment.SetEnvironmentVariable(environmentVariable, connectionString);

        try
        {
            var column = new ColumnProcessingOptions
            {
                Ordinal = 1,
                ColumnName = "display_name",
                DataType = "Text",
                MaxLength = 64,
                Unicode = true
            };
            var table = new TableProcessingOptions
            {
                SchemaName = "public",
                TableName = "customer_data",
                Columns = { column }
            };
            var configuration = new AnonymizationConfiguration
            {
                Database = new DatabaseTargetConfiguration
                {
                    DatabaseEngine = "PostgreSql",
                    DatabaseName = "anonymyzer_test",
                    DetachedCopyMarkerId = "11111111-2222-3333-4444-555555555555"
                },
                Tables = { table }
            };

            IReadOnlyList<ColumnSample> samples = await new ColumnSampleReader().ReadAsync(
                configuration,
                table,
                column,
                environmentVariable,
                maximumRows: 2,
                TestContext.Current.CancellationToken);

            Assert.Equal(new[] { "Ada", "Grace" }, samples.Select(sample => sample.Value).Order());
            Assert.All(samples, sample => Assert.NotNull(sample.Value));
        }
        finally
        {
            Environment.SetEnvironmentVariable(environmentVariable, previousValue);
        }
    }

    [Fact]
    public async Task RejectsSampleSizeAboveSafetyLimit()
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            new ColumnSampleReader().ReadAsync(
                new AnonymizationConfiguration(),
                new TableProcessingOptions(),
                new ColumnProcessingOptions(),
                "UNUSED_CONNECTION",
                maximumRows: 51,
                TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ListsOnlyUnconfiguredNonPrimaryKeyColumnsFromValidatedClone()
    {
        string connectionString = RequireConnectionString();
        const string environmentVariable = "ANONYMYZER_METADATA_TEST_CONNECTION";
        string? previousValue = Environment.GetEnvironmentVariable(environmentVariable);
        Environment.SetEnvironmentVariable(environmentVariable, connectionString);

        try
        {
            var table = new TableProcessingOptions
            {
                SchemaName = "public",
                TableName = "customer_data",
                Columns =
                {
                    new ColumnProcessingOptions { Ordinal = 2, ColumnName = "display_name", DataType = "Text" }
                }
            };
            var configuration = new AnonymizationConfiguration
            {
                Database = new DatabaseTargetConfiguration
                {
                    DatabaseEngine = "PostgreSql",
                    DatabaseName = "anonymyzer_test",
                    DetachedCopyMarkerId = "11111111-2222-3333-4444-555555555555"
                },
                Tables = { table }
            };

            IReadOnlyList<AvailableColumn> columns = await new ColumnMetadataReader().ReadAvailableAsync(
                configuration,
                table,
                environmentVariable,
                TestContext.Current.CancellationToken);

            Assert.DoesNotContain(columns, column => column.ColumnName is "id" or "display_name");
            Assert.Contains(columns, column => column is { ColumnName: "pesel", DataType: "Integer" });
            Assert.Contains(columns, column => column is { ColumnName: "preferences", DataType: "Json" });
        }
        finally
        {
            Environment.SetEnvironmentVariable(environmentVariable, previousValue);
        }
    }

    private static string RequireConnectionString()
    {
        string? connectionString = Environment.GetEnvironmentVariable("ANONYMYZER_POSTGRES_CONNECTION");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            Assert.Skip("Set ANONYMYZER_POSTGRES_CONNECTION to run the PostgreSQL sample-reader integration test.");
        }

        return connectionString;
    }
}
