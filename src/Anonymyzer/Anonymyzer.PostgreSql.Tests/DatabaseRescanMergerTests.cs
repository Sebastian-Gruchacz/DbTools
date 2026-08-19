namespace Anonymyzer.PostgreSql.Tests;

using Anonymyzer.Configuration;

public sealed class DatabaseRescanMergerTests
{
    [Fact]
    public void RefreshesDetectionAndMetadataWithoutOverwritingOperatorChoices()
    {
        var configuredColumn = new ColumnProcessingOptions
        {
            Ordinal = 9,
            ColumnName = "email",
            DataType = "Text",
            MaxLength = 100,
            Enabled = true,
            SemanticRole = "Custom.Email",
            OperatorOverrides = new ColumnOperatorOverrides { SemanticRole = true, Generator = true },
            Generator = new ColumnGeneratorConfiguration { GeneratorType = "FixedText", ProfileId = "custom" }
        };
        AnonymizationConfiguration configuration = ConfigurationWith("people", configuredColumn);
        TableProcessingOptions fresh = Table("people", new ColumnProcessingOptions
        {
            Ordinal = 2,
            ColumnName = "email",
            DataType = "Text",
            MaxLength = 320,
            Detection = new CandidateDetectionConfiguration { IsCandidate = true, SuggestedRole = "Contact.Email" },
            Generator = new ColumnGeneratorConfiguration { GeneratorType = "TextShuffler", ProfileId = "default" }
        });
        fresh.PrimaryKeyColumns = ["tenant_id", "id"];

        DatabaseRescanMergeResult result = new DatabaseRescanMerger().Merge(configuration, [fresh]);

        Assert.Equal(1, result.RefreshedColumns);
        Assert.Equal(1, result.PreservedSelections);
        Assert.Equal(2, configuredColumn.Ordinal);
        Assert.Equal(320, configuredColumn.MaxLength);
        Assert.Equal("Contact.Email", configuredColumn.Detection.SuggestedRole);
        Assert.Equal("Custom.Email", configuredColumn.SemanticRole);
        Assert.Equal("FixedText", configuredColumn.Generator.GeneratorType);
        Assert.Equal(["tenant_id", "id"], configuration.Tables.Single().PrimaryKeyColumns);
    }

    [Fact]
    public void AddsNewObjectsAndRetainsMissingObjectsAsWarnings()
    {
        AnonymizationConfiguration configuration = ConfigurationWith(
            "old_table",
            new ColumnProcessingOptions { ColumnName = "old_column" });
        TableProcessingOptions fresh = Table(
            "new_table",
            new ColumnProcessingOptions { ColumnName = "new_column", Ordinal = 1 });

        DatabaseRescanMergeResult result = new DatabaseRescanMerger().Merge(configuration, [fresh]);

        Assert.Equal(1, result.AddedTables);
        Assert.Equal(1, result.AddedColumns);
        Assert.Equal(1, result.MissingTables);
        Assert.Equal(1, result.MissingColumns);
        Assert.Contains(configuration.Tables, table => table.TableName == "old_table" && table.SchemaStatus == "Missing");
        Assert.Contains(configuration.Tables, table => table.TableName == "new_table" && table.SchemaStatus == "Current");
    }

    [Fact]
    public void RefreshesAutomaticGeneratorWhenNoOperatorSelectionExists()
    {
        var configuredColumn = new ColumnProcessingOptions
        {
            ColumnName = "value",
            Generator = new ColumnGeneratorConfiguration { GeneratorType = "LegacyAutomatic" }
        };
        AnonymizationConfiguration configuration = ConfigurationWith("items", configuredColumn);
        TableProcessingOptions fresh = Table("items", new ColumnProcessingOptions
        {
            ColumnName = "value",
            Generator = new ColumnGeneratorConfiguration { GeneratorType = "TextShuffler" }
        });

        DatabaseRescanMergeResult result = new DatabaseRescanMerger().Merge(configuration, [fresh]);

        Assert.Equal(0, result.PreservedSelections);
        Assert.Equal("TextShuffler", configuredColumn.Generator.GeneratorType);
        Assert.Equal("Current", configuredColumn.SchemaStatus);
    }

    private static AnonymizationConfiguration ConfigurationWith(
        string tableName,
        ColumnProcessingOptions column) => new()
    {
        Tables = { Table(tableName, column) }
    };

    private static TableProcessingOptions Table(string tableName, params ColumnProcessingOptions[] columns) => new()
    {
        SchemaName = "public",
        TableName = tableName,
        Columns = columns.ToList()
    };
}
