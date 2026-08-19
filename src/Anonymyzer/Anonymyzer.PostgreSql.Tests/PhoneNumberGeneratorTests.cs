namespace Anonymyzer.PostgreSql.Tests;

using System.Text.RegularExpressions;
using Anonymyzer.Base.Generation;
using Anonymyzer.Generators.Simple;
using Anonymyzer.LanguagePack.English;
using Anonymyzer.LanguagePack.Polish;

public class PhoneNumberGeneratorTests
{
    [Theory]
    [InlineData("pl-PL", PhoneNumberFormat.National, @"^501 \d{3} \d{3}$")]
    [InlineData("pl-PL", PhoneNumberFormat.International, @"^\+48 501 \d{3} \d{3}$")]
    [InlineData("en-US", PhoneNumberFormat.National, @"^\(202\) 555-01\d{2}$")]
    [InlineData("en-US", PhoneNumberFormat.International, @"^\+1 202-555-01\d{2}$")]
    public async Task GeneratesLocaleSpecificFormat(
        string locale,
        PhoneNumberFormat format,
        string expectedPattern)
    {
        var generator = CreateGenerator();
        var configuration = new PhoneNumberGeneratorConfiguration
        {
            Locale = locale,
            Format = format,
            Seed = 42,
            PreserveNulls = false
        };
        GeneratorBinding binding = Bind();

        Assert.Empty(generator.GetDataRequirements(binding, configuration));
        await using IGeneratorSession session = await generator.PrepareAsync(
            new GeneratorPreparationContext(binding, new RejectingDataReader()),
            configuration,
            TestContext.Current.CancellationToken);
        var row = new FakeGeneratorRow(null);

        await session.ApplyAsync(row, TestContext.Current.CancellationToken);

        Assert.Matches(new Regex(expectedPattern, RegexOptions.CultureInvariant), Assert.IsType<string>(row.Value));
    }

    [Fact]
    public async Task IsDeterministicAndPreservesNulls()
    {
        PhoneNumberGenerator generator = CreateGenerator();
        var configuration = new PhoneNumberGeneratorConfiguration { Locale = "pl-PL", Seed = 7 };
        GeneratorBinding binding = Bind();
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await using IGeneratorSession firstSession = await generator.PrepareAsync(
            new GeneratorPreparationContext(binding, new RejectingDataReader()),
            configuration,
            cancellationToken);
        await using IGeneratorSession secondSession = await generator.PrepareAsync(
            new GeneratorPreparationContext(binding, new RejectingDataReader()),
            configuration,
            cancellationToken);
        var first = new FakeGeneratorRow("old");
        var repeated = new FakeGeneratorRow("old");
        var empty = new FakeGeneratorRow(null);

        await firstSession.ApplyAsync(first, cancellationToken);
        await firstSession.ApplyAsync(empty, cancellationToken);
        await secondSession.ApplyAsync(repeated, cancellationToken);

        Assert.Equal(first.Value, repeated.Value);
        Assert.Null(empty.Value);
    }

    [Fact]
    public async Task RejectsLocaleWithoutInstalledProvider()
    {
        PhoneNumberGenerator generator = CreateGenerator();
        var configuration = new PhoneNumberGeneratorConfiguration { Locale = "de-DE" };

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await generator.PrepareAsync(
                new GeneratorPreparationContext(Bind(), new RejectingDataReader()),
                configuration,
                TestContext.Current.CancellationToken));

        Assert.Contains("not installed", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RefusesToRepeatTheReservedUsRange()
    {
        PhoneNumberGenerator generator = CreateGenerator();
        var configuration = new PhoneNumberGeneratorConfiguration
        {
            Locale = "en-US",
            PreserveNulls = false
        };
        await using IGeneratorSession session = await generator.PrepareAsync(
            new GeneratorPreparationContext(Bind(), new RejectingDataReader()),
            configuration,
            TestContext.Current.CancellationToken);
        var values = new HashSet<object?>();

        for (int index = 0; index < 100; index++)
        {
            var row = new FakeGeneratorRow(null);
            await session.ApplyAsync(row, TestContext.Current.CancellationToken);
            Assert.True(values.Add(row.Value));
        }

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await session.ApplyAsync(new FakeGeneratorRow(null), TestContext.Current.CancellationToken));
    }

    private static PhoneNumberGenerator CreateGenerator() => new(
    [
        new PolishPhoneNumberLocaleDataProvider(),
        new EnglishPhoneNumberLocaleDataProvider()
    ]);

    private static GeneratorBinding Bind() => new(
        new GeneratorTableReference("public", "people"),
        new Dictionary<string, string> { [PhoneNumberGenerator.ValueOutput] = "phone" });

    private sealed class RejectingDataReader : IGeneratorDataReader
    {
        public IAsyncEnumerable<GeneratorDataRow> ReadAsync(
            GeneratorDataRequirement requirement,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("PhoneNumber must not read source data.");
        }
    }

    private sealed class FakeGeneratorRow(object? value) : IGeneratorRow
    {
        public object? Value { get; private set; } = value;

        public object? GetValue(string columnName) => Value;

        public void SetValue(string columnName, object? newValue) => Value = newValue;
    }
}
