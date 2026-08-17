namespace Anonymyzer.PostgreSql.Tests;

using Anonymyzer.Base;
using Anonymyzer.Base.Detection;
using Anonymyzer.Console.GenerateConfiguration;
using Anonymyzer.LanguagePack.English;
using Anonymyzer.LanguagePack.Polish;

public sealed class ColumnConfigurationBuilderTests
{
    [Fact]
    public void IncludesNonTextCandidatesWithoutAssigningTextGenerator()
    {
        var table = new StubTable("people", "dbo", 10);
        var engine = new StubEngine(
            table,
            new StubColumn(1, "id", DbDataType.Integer, isPrimaryKey: true),
            new StubColumn(2, "PESEL", DbDataType.Integer),
            new StubColumn(3, "notes", DbDataType.Text, maxLength: 200),
            new StubColumn(4, "PeselJakoNrKontrahenta", DbDataType.Boolean));
        var detector = new ColumnCandidateDetector(new IColumnCandidateRuleProvider[]
        {
            new EnglishColumnCandidateRuleProvider(),
            new PolishColumnCandidateRuleProvider()
        });

        var result = new ColumnConfigurationBuilder(detector).CreateTable(engine, table);

        Assert.Equal(3, result.Columns.Count);
        var pesel = Assert.Single(result.Columns, column => column.ColumnName == "PESEL");
        Assert.Equal(2, pesel.Ordinal);
        Assert.Equal(nameof(DbDataType.Integer), pesel.DataType);
        Assert.True(pesel.Detection.IsCandidate);
        Assert.Equal("Person.NationalId", pesel.Detection.SuggestedRole);
        Assert.Equal(string.Empty, pesel.Generator.GeneratorType);

        var notes = Assert.Single(result.Columns, column => column.ColumnName == "notes");
        Assert.Equal("TextShuffler", notes.Generator.GeneratorType);
        Assert.Equal("TextShuffler:Default", notes.Generator.ProfileId);

        var booleanSetting = Assert.Single(result.Columns, column => column.ColumnName == "PeselJakoNrKontrahenta");
        Assert.False(booleanSetting.Detection.IsCandidate);
    }

    private sealed class StubEngine(ITableInfo table, params IColumnInfo[] columns) : IAnonymyzerEngine
    {
        public IEnumerable<ITableInfo> ListTables(bool listSystemTables = false) => new[] { table };
        public IEnumerable<IColumnInfo> ListColumns(ITableInfo tableInfo) => columns;
    }

    private sealed record StubTable(string Name, string SchemaName, long EstimatedRowCount) : ITableInfo;

    private sealed class StubColumn(
        int ordinal,
        string name,
        DbDataType dataType,
        bool isPrimaryKey = false,
        int maxLength = 0) : IColumnInfo
    {
        public int Ordinal { get; } = ordinal;
        public string Name { get; } = name;
        public DbDataType DataType { get; } = dataType;
        public bool IsNullable => true;
        public bool IsPartOfThePrimaryKey { get; } = isPrimaryKey;
        public bool IsUnicodeText => DataType == DbDataType.Text;
        public int MaxLength { get; } = maxLength;
    }
}
