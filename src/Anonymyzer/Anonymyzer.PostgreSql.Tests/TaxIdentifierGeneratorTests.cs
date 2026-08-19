namespace Anonymyzer.PostgreSql.Tests;

using Anonymyzer.Base.Generation;
using Anonymyzer.Generators.Simple;
using Anonymyzer.LanguagePack.Polish;

public class TaxIdentifierGeneratorTests
{
    [Theory]
    [InlineData(TaxIdentifierFormat.DigitsOnly, 10)]
    [InlineData(TaxIdentifierFormat.Hyphenated, 13)]
    [InlineData(TaxIdentifierFormat.International, 12)]
    public async Task GeneratesChecksumValidPolishNip(TaxIdentifierFormat format, int expectedLength)
    {
        var configuration = new TaxIdentifierGeneratorConfiguration
        {
            Locale = "pl-PL",
            Format = format,
            Seed = 73,
            PreserveNulls = false
        };
        await using IGeneratorSession session = await PrepareAsync(configuration);
        var row = new FakeGeneratorRow(null);

        await session.ApplyAsync(row, TestContext.Current.CancellationToken);

        string formatted = Assert.IsType<string>(row.Value);
        string digits = new(formatted.Where(char.IsAsciiDigit).ToArray());
        Assert.Equal(expectedLength, formatted.Length);
        Assert.True(PolishTaxIdentifierLocaleDataProvider.IsValidNip(digits));
    }

    [Fact]
    public async Task ProducesDistinctValuesForASequence()
    {
        var configuration = new TaxIdentifierGeneratorConfiguration
        {
            Locale = "pl-PL",
            Seed = -123,
            PreserveNulls = false
        };
        await using IGeneratorSession session = await PrepareAsync(configuration);
        var values = new HashSet<object?>();

        for (int index = 0; index < 1000; index++)
        {
            var row = new FakeGeneratorRow(null);
            await session.ApplyAsync(row, TestContext.Current.CancellationToken);
            Assert.True(values.Add(row.Value));
        }
    }

    [Fact]
    public async Task SameSeedIsDeterministicAndPreservedNullDoesNotConsumeAValue()
    {
        var configuration = new TaxIdentifierGeneratorConfiguration { Locale = "pl-PL", Seed = 17 };
        await using IGeneratorSession withNull = await PrepareAsync(configuration);
        await using IGeneratorSession direct = await PrepareAsync(configuration);
        var empty = new FakeGeneratorRow(null);
        var afterNull = new FakeGeneratorRow("old");
        var expected = new FakeGeneratorRow("old");

        await withNull.ApplyAsync(empty, TestContext.Current.CancellationToken);
        await withNull.ApplyAsync(afterNull, TestContext.Current.CancellationToken);
        await direct.ApplyAsync(expected, TestContext.Current.CancellationToken);

        Assert.Null(empty.Value);
        Assert.Equal(expected.Value, afterNull.Value);
    }

    [Fact]
    public async Task RejectsLocaleWithoutInstalledProvider()
    {
        var generator = CreateGenerator();
        var configuration = new TaxIdentifierGeneratorConfiguration { Locale = "en-US" };

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await generator.PrepareAsync(
                new GeneratorPreparationContext(Bind(), new RejectingDataReader()),
                configuration,
                TestContext.Current.CancellationToken));

        Assert.Contains("not installed", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("")]
    [InlineData("1234567890")]
    [InlineData("123-456-78-90")]
    public void ValidatorRejectsMalformedOrIncorrectNip(string value)
    {
        Assert.False(PolishTaxIdentifierLocaleDataProvider.IsValidNip(value));
    }

    [Theory]
    [InlineData("REGON9", 9)]
    [InlineData("REGON14", 14)]
    public async Task GeneratesChecksumValidRegon(string variant, int length)
    {
        var configuration = new TaxIdentifierGeneratorConfiguration
        {
            Variant = variant,
            Format = TaxIdentifierFormat.DigitsOnly,
            PreserveNulls = false
        };
        await using IGeneratorSession session = await PrepareAsync(configuration);
        var row = new FakeGeneratorRow(null);

        await session.ApplyAsync(row, TestContext.Current.CancellationToken);

        string value = Assert.IsType<string>(row.Value);
        Assert.Equal(length, value.Length);
        Assert.True(PolishTaxIdentifierLocaleDataProvider.IsValidRegon(value));
    }

    [Fact]
    public async Task RejectsFormattedRegon()
    {
        var configuration = new TaxIdentifierGeneratorConfiguration
        {
            Variant = "REGON9",
            Format = TaxIdentifierFormat.Hyphenated
        };

        await Assert.ThrowsAsync<InvalidOperationException>(async () => await PrepareAsync(configuration));
    }

    private static async ValueTask<IGeneratorSession> PrepareAsync(
        TaxIdentifierGeneratorConfiguration configuration)
    {
        TaxIdentifierGenerator generator = CreateGenerator();
        GeneratorBinding binding = Bind();
        Assert.Empty(generator.GetDataRequirements(binding, configuration));
        return await generator.PrepareAsync(
            new GeneratorPreparationContext(binding, new RejectingDataReader()),
            configuration,
            TestContext.Current.CancellationToken);
    }

    private static TaxIdentifierGenerator CreateGenerator() =>
        new([new PolishTaxIdentifierLocaleDataProvider()]);

    private static GeneratorBinding Bind() => new(
        new GeneratorTableReference("public", "companies"),
        new Dictionary<string, string> { [TaxIdentifierGenerator.ValueOutput] = "tax_id" });

    private sealed class RejectingDataReader : IGeneratorDataReader
    {
        public IAsyncEnumerable<GeneratorDataRow> ReadAsync(
            GeneratorDataRequirement requirement,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("TaxIdentifier must not read source data.");
        }
    }

    private sealed class FakeGeneratorRow(object? value) : IGeneratorRow
    {
        public object? Value { get; private set; } = value;

        public object? GetValue(string columnName) => Value;

        public void SetValue(string columnName, object? newValue) => Value = newValue;
    }
}
