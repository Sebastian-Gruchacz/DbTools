namespace Anonymyzer.PostgreSql.Tests;

using Anonymyzer.Base.Generation;
using Anonymyzer.Generators.Simple;
using Anonymyzer.LanguagePack.Polish;

public class BankAccountGeneratorTests
{
    [Theory]
    [InlineData(BankAccountFormat.IbanCompact, 28)]
    [InlineData(BankAccountFormat.IbanGrouped, 34)]
    [InlineData(BankAccountFormat.DomesticNrb, 26)]
    public async Task GeneratesChecksumValidPolishAccount(
        BankAccountFormat format,
        int expectedLength)
    {
        var configuration = new BankAccountGeneratorConfiguration
        {
            Format = format,
            Seed = 73,
            PreserveNulls = false
        };
        await using IGeneratorSession session = await PrepareAsync(configuration);
        var row = new FakeGeneratorRow(null);

        await session.ApplyAsync(row, TestContext.Current.CancellationToken);

        string value = Assert.IsType<string>(row.Value);
        Assert.Equal(expectedLength, value.Length);
        if (format == BankAccountFormat.DomesticNrb)
        {
            Assert.True(PolishBankAccountLocaleDataProvider.IsValidPolishNrb(value));
        }
        else
        {
            Assert.True(PolishBankAccountLocaleDataProvider.IsValidPolishIban(value));
        }
    }

    [Fact]
    public async Task UsesNonRoutableBankAndBranchSegmentAndDistinctSequence()
    {
        var configuration = new BankAccountGeneratorConfiguration
        {
            PreserveNulls = false,
            Seed = -123
        };
        await using IGeneratorSession session = await PrepareAsync(configuration);
        var values = new HashSet<string>();

        for (int index = 0; index < 1000; index++)
        {
            var row = new FakeGeneratorRow(null);
            await session.ApplyAsync(row, TestContext.Current.CancellationToken);
            string value = Assert.IsType<string>(row.Value);
            Assert.Equal("00000000", value[4..12]);
            Assert.True(values.Add(value));
        }
    }

    [Fact]
    public async Task PreservedNullDoesNotConsumeAValue()
    {
        var configuration = new BankAccountGeneratorConfiguration { Seed = 17 };
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
        var configuration = new BankAccountGeneratorConfiguration { Locale = "en-US" };

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await PrepareAsync(configuration));

        Assert.Contains("not installed", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("")]
    [InlineData("PL00000000000000000000000000")]
    [InlineData("PL6110901014000007121981287X")]
    public void ValidatorRejectsMalformedOrIncorrectIban(string value)
    {
        Assert.False(PolishBankAccountLocaleDataProvider.IsValidPolishIban(value));
    }

    private static async ValueTask<IGeneratorSession> PrepareAsync(
        BankAccountGeneratorConfiguration configuration)
    {
        var generator = new BankAccountGenerator([new PolishBankAccountLocaleDataProvider()]);
        GeneratorBinding binding = new(
            new GeneratorTableReference("public", "payments"),
            new Dictionary<string, string> { [BankAccountGenerator.ValueOutput] = "bank_account" });
        Assert.Empty(generator.GetDataRequirements(binding, configuration));
        return await generator.PrepareAsync(
            new GeneratorPreparationContext(binding, new RejectingDataReader()),
            configuration,
            TestContext.Current.CancellationToken);
    }

    private sealed class RejectingDataReader : IGeneratorDataReader
    {
        public IAsyncEnumerable<GeneratorDataRow> ReadAsync(
            GeneratorDataRequirement requirement,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("BankAccount must not read source data.");
        }
    }

    private sealed class FakeGeneratorRow(object? value) : IGeneratorRow
    {
        public object? Value { get; private set; } = value;

        public object? GetValue(string columnName) => Value;

        public void SetValue(string columnName, object? newValue) => Value = newValue;
    }
}
