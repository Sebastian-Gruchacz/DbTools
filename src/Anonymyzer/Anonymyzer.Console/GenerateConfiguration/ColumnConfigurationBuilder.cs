namespace Anonymyzer.Console.GenerateConfiguration;

using Anonymyzer.Base;
using Anonymyzer.Configuration;

internal sealed class ColumnConfigurationBuilder(ColumnCandidateDetector candidateDetector)
{
    private const string DefaultProfile = "Default";

    public TableProcessingOptions CreateTable(IAnonymyzerEngine engine, ITableInfo tableInfo)
    {
        var config = TableProcessingOptions.DefaultForTable(tableInfo.Name, tableInfo.SchemaName);
        foreach (IColumnInfo column in engine.ListColumns(tableInfo).Where(column => !column.IsPartOfThePrimaryKey))
        {
            config.Columns.Add(new ColumnProcessingOptions
            {
                Ordinal = column.Ordinal,
                ColumnName = column.Name,
                DataType = column.DataType.ToString(),
                MaxLength = column.MaxLength,
                Unicode = column.IsUnicodeText,
                Enabled = false,
                Detection = candidateDetector.Detect(column.Name),
                Generator = CreateDefaultGenerator(column.DataType)
            });
        }

        return config;
    }

    private static ColumnGeneratorConfiguration CreateDefaultGenerator(DbDataType dataType)
    {
        return dataType == DbDataType.Text
            ? new ColumnGeneratorConfiguration
            {
                GeneratorType = "TextShuffler",
                GeneratorVersion = "1.0.0",
                ProfileId = $"TextShuffler:{DefaultProfile}"
            }
            : new ColumnGeneratorConfiguration();
    }
}
