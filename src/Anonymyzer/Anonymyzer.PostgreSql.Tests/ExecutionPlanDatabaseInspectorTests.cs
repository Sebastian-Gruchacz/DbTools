namespace Anonymyzer.PostgreSql.Tests;

using Anonymyzer.Base;
using Anonymyzer.Base.Generation;
using Anonymyzer.Configuration;
using Anonymyzer.Console.Planning;
using Anonymyzer.Generators.Simple;

public sealed class ExecutionPlanDatabaseInspectorTests
{
    [Fact]
    public void ValidatesLiveSchemaAndEstimatesCompleteScanMemory()
    {
        var generator = new ShufflingTextGenerator();
        AnonymizationConfiguration configuration = CreateConfiguration(generator, maxLength: 64);
        AnonymizationExecutionPlan plan = new AnonymizationExecutionPlanner(new[] { generator }).Build(configuration);
        var engine = new StubEngine(
            new StubTable("notes", "public", 25),
            new StubColumn("value", maxLength: 64, unicode: true));

        ExecutionPlanDatabaseInspection inspection = new ExecutionPlanDatabaseInspector()
            .Inspect(configuration, plan, engine);

        GeneratorStepDatabaseInspection step = Assert.Single(inspection.Steps).Value;
        Assert.Equal(25, step.EstimatedTargetRows);
        DataRequirementEstimate requirement = Assert.Single(step.DataRequirements).Value;
        Assert.Equal(25, requirement.EstimatedRows);
        Assert.Equal(4000, requirement.EstimatedMaximumMemoryBytes);

        IReadOnlyList<string> output = ExecutionPlanFormatter.Format(plan, inspection);
        Assert.Contains(output, line => line.Contains("estimated target rows: 25", StringComparison.Ordinal));
        Assert.Contains(output, line => line.Contains("rough max memory 3.91 KiB", StringComparison.Ordinal));
    }

    [Fact]
    public void RejectsSchemaDriftBeforeExecution()
    {
        var generator = new ShufflingTextGenerator();
        AnonymizationConfiguration configuration = CreateConfiguration(generator, maxLength: 64);
        AnonymizationExecutionPlan plan = new AnonymizationExecutionPlanner(new[] { generator }).Build(configuration);
        var engine = new StubEngine(
            new StubTable("notes", "public", 25),
            new StubColumn("value", maxLength: 32, unicode: true));

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            new ExecutionPlanDatabaseInspector().Inspect(configuration, plan, engine));

        Assert.Contains("Schema drift", exception.Message);
        Assert.Contains("Text(64)", exception.Message);
        Assert.Contains("Text(32)", exception.Message);
    }

    [Fact]
    public void ReportsUnknownMemoryForUnboundedText()
    {
        var generator = new ShufflingTextGenerator();
        AnonymizationConfiguration configuration = CreateConfiguration(generator, maxLength: 0);
        AnonymizationExecutionPlan plan = new AnonymizationExecutionPlanner(new[] { generator }).Build(configuration);
        var engine = new StubEngine(
            new StubTable("notes", "public", 25),
            new StubColumn("value", maxLength: 0, unicode: true));

        ExecutionPlanDatabaseInspection inspection = new ExecutionPlanDatabaseInspector()
            .Inspect(configuration, plan, engine);

        Assert.Null(Assert.Single(Assert.Single(inspection.Steps).Value.DataRequirements).Value.EstimatedMaximumMemoryBytes);
        Assert.Contains(
            ExecutionPlanFormatter.Format(plan, inspection),
            line => line.Contains("unknown (unbounded text column)", StringComparison.Ordinal));
    }

    private static AnonymizationConfiguration CreateConfiguration(
        ShufflingTextGenerator generator,
        int maxLength)
    {
        return new AnonymizationConfiguration
        {
            GeneratorProfiles =
            {
                new GeneratorProfileConfiguration
                {
                    Id = "shuffle",
                    GeneratorType = generator.Descriptor.Type,
                    GeneratorVersion = generator.Descriptor.Version,
                    Options = generator.Configuration.Serialize(generator.Configuration.CreateDefault())
                }
            },
            Tables =
            {
                new TableProcessingOptions
                {
                    SchemaName = "public",
                    TableName = "notes",
                    Enabled = true,
                    Columns =
                    {
                        new ColumnProcessingOptions
                        {
                            Ordinal = 1,
                            ColumnName = "value",
                            DataType = "Text",
                            MaxLength = maxLength,
                            Unicode = true,
                            Enabled = true,
                            Generator = new ColumnGeneratorConfiguration
                            {
                                GeneratorType = generator.Descriptor.Type,
                                GeneratorVersion = generator.Descriptor.Version,
                                ProfileId = "shuffle"
                            }
                        }
                    }
                }
            }
        };
    }

    private sealed class StubEngine(ITableInfo table, IColumnInfo column) : IAnonymyzerEngine
    {
        public IEnumerable<ITableInfo> ListTables(bool listSystemTables = false) => new[] { table };

        public IEnumerable<IColumnInfo> ListColumns(ITableInfo tableInfo) => new[] { column };

        public IEnumerable<ForeignKeyInfo> ListForeignKeys(ITableInfo tableInfo) => [];
    }

    private sealed record StubTable(string Name, string SchemaName, long EstimatedRowCount) : ITableInfo;

    private sealed class StubColumn(string name, int maxLength, bool unicode) : IColumnInfo
    {
        public int Ordinal => 1;
        public string Name { get; } = name;
        public DbDataType DataType => DbDataType.Text;
        public bool IsNullable => true;
        public bool IsPartOfThePrimaryKey => false;
        public bool IsUnicodeText { get; } = unicode;
        public int MaxLength { get; } = maxLength;
    }
}
