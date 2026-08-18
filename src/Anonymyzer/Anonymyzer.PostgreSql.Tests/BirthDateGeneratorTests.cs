namespace Anonymyzer.PostgreSql.Tests;

using Anonymyzer.Base;
using Anonymyzer.Base.Generation;
using Anonymyzer.Generators.Person;

public class BirthDateGeneratorTests
{
    [Fact]
    public async Task GeneratesDeterministicDatesWithinConfiguredRangeAndPreservesNulls()
    {
        var configuration = new BirthDateGeneratorConfiguration
        {
            MinimumDate = "1980-01-01",
            MaximumDate = "1980-01-31",
            Seed = 42
        };
        await using IGeneratorSession firstSession = await PrepareAsync(configuration);
        await using IGeneratorSession repeatedSession = await PrepareAsync(configuration);
        var nullRow = new FakeRow(null);
        var first = new FakeRow(new DateOnly(2000, 1, 1));
        var repeated = new FakeRow(new DateOnly(2000, 1, 1));

        await firstSession.ApplyAsync(nullRow, TestContext.Current.CancellationToken);
        await firstSession.ApplyAsync(first, TestContext.Current.CancellationToken);
        await repeatedSession.ApplyAsync(repeated, TestContext.Current.CancellationToken);

        Assert.Null(nullRow.Value);
        DateOnly date = Assert.IsType<DateOnly>(first.Value);
        Assert.InRange(date, new DateOnly(1980, 1, 1), new DateOnly(1980, 1, 31));
        Assert.Equal(first.Value, repeated.Value);
    }

    [Fact]
    public void DescriptorSupportsDateAndDateTimeColumns()
    {
        var generator = new BirthDateGenerator();

        Assert.Equal("Person.BirthDate", Assert.Single(generator.Descriptor.Outputs).SemanticRole);
        Assert.Equal([DbDataType.Date, DbDataType.DateTime], generator.Descriptor.SupportedDataTypes);
    }

    [Fact]
    public void CodecRejectsInvalidOrReversedRange()
    {
        var codec = new BirthDateGeneratorConfigurationCodec();

        Assert.Single(codec.Validate(new BirthDateGeneratorConfiguration { MinimumDate = "invalid" }));
        Assert.Single(codec.Validate(new BirthDateGeneratorConfiguration
        {
            MinimumDate = "2000-01-02",
            MaximumDate = "2000-01-01"
        }));
    }

    private static async ValueTask<IGeneratorSession> PrepareAsync(BirthDateGeneratorConfiguration configuration)
    {
        var generator = new BirthDateGenerator();
        GeneratorBinding binding = new(
            new GeneratorTableReference("public", "people"),
            new Dictionary<string, string> { [BirthDateGenerator.ValueOutput] = "birth_date" });
        return await generator.PrepareAsync(
            new GeneratorPreparationContext(binding, new RejectingDataReader()),
            configuration,
            TestContext.Current.CancellationToken);
    }

    private sealed class RejectingDataReader : IGeneratorDataReader
    {
        public IAsyncEnumerable<GeneratorDataRow> ReadAsync(
            GeneratorDataRequirement requirement,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("BirthDate must not read source data.");
    }

    private sealed class FakeRow(object? value) : IGeneratorRow
    {
        public object? Value { get; private set; } = value;

        public object? GetValue(string columnName) => Value;

        public void SetValue(string columnName, object? newValue) => Value = newValue;
    }
}
