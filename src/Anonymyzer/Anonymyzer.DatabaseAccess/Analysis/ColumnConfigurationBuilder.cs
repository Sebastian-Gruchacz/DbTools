namespace Anonymyzer.Console.GenerateConfiguration;

using Anonymyzer.Base;
using Anonymyzer.Configuration;

public sealed class ColumnConfigurationBuilder(ColumnCandidateDetector candidateDetector)
{
    private const string DefaultProfile = "Default";

    public TableProcessingOptions CreateTable(IAnonymyzerEngine engine, ITableInfo tableInfo)
    {
        var config = TableProcessingOptions.DefaultForTable(tableInfo.Name, tableInfo.SchemaName);
        IColumnInfo[] columns = engine.ListColumns(tableInfo).ToArray();
        config.PrimaryKeyColumns = columns
            .Where(column => column.IsPartOfThePrimaryKey)
            .OrderBy(column => column.Ordinal)
            .Select(column => column.Name)
            .ToList();
        foreach (IColumnInfo column in columns.Where(column => !column.IsPartOfThePrimaryKey))
        {
            config.Columns.Add(new ColumnProcessingOptions
            {
                Ordinal = column.Ordinal,
                ColumnName = column.Name,
                DataType = column.DataType.ToString(),
                MaxLength = column.MaxLength,
                Unicode = column.IsUnicodeText,
                Enabled = false,
                Detection = candidateDetector.Detect(column.Name, column.DataType),
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
