namespace Anonymyzer.PostgreSql.Tests;

using Anonymyzer.Base.Generation;
using Anonymyzer.Generators.Simple;
using Anonymyzer.LanguagePack.English;
using Anonymyzer.LanguagePack.Polish;

public class CompanyNameGeneratorTests
{
    [Theory]
    [InlineData("pl-PL")]
    [InlineData("en-US")]
    public async Task GeneratesDeterministicMarkedUniqueNames(string locale)
    {
        var configuration = new CompanyNameGeneratorConfiguration
        {
            Locale = locale,
            SyntheticMarker = "FAKE",
            Seed = 77,
            PreserveNulls = false
        };
        await using IGeneratorSession firstSession = await PrepareAsync(configuration);
        await using IGeneratorSession repeatedSession = await PrepareAsync(configuration);
        var first = new FakeRow(null);
        var second = new FakeRow(null);
        var repeated = new FakeRow(null);

        await firstSession.ApplyAsync(first, TestContext.Current.CancellationToken);
        await firstSession.ApplyAsync(second, TestContext.Current.CancellationToken);
        await repeatedSession.ApplyAsync(repeated, TestContext.Current.CancellationToken);

        string firstName = Assert.IsType<string>(first.Value);
        Assert.Contains("FAKE 000001", firstName);
        Assert.Contains("FAKE 000002", Assert.IsType<string>(second.Value));
        Assert.NotEqual(first.Value, second.Value);
        Assert.Equal(first.Value, repeated.Value);
    }

    [Fact]
    public async Task PreservesNullWithoutConsumingSequenceNumber()
    {
        var configuration = new CompanyNameGeneratorConfiguration { Seed = 5 };
        await using IGeneratorSession session = await PrepareAsync(configuration);
        var empty = new FakeRow(null);
        var populated = new FakeRow("old");

        await session.ApplyAsync(empty, TestContext.Current.CancellationToken);
        await session.ApplyAsync(populated, TestContext.Current.CancellationToken);

        Assert.Null(empty.Value);
        Assert.Contains("TEST 000001", Assert.IsType<string>(populated.Value));
    }

    [Fact]
    public void CodecRequiresVisibleSyntheticMarker()
    {
        var codec = new CompanyNameGeneratorConfigurationCodec();

        Assert.Single(codec.Validate(new CompanyNameGeneratorConfiguration { SyntheticMarker = " " }));
    }

    private static async ValueTask<IGeneratorSession> PrepareAsync(CompanyNameGeneratorConfiguration configuration)
    {
        var generator = new CompanyNameGenerator(
        [
            new PolishCompanyNameLocaleDataProvider(),
            new EnglishCompanyNameLocaleDataProvider()
        ]);
        GeneratorBinding binding = new(
            new GeneratorTableReference("public", "companies"),
            new Dictionary<string, string> { [CompanyNameGenerator.ValueOutput] = "company_name" });
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
            throw new InvalidOperationException("CompanyName must not read source data.");
    }

    private sealed class FakeRow(object? value) : IGeneratorRow
    {
        public object? Value { get; private set; } = value;

        public object? GetValue(string columnName) => Value;

        public void SetValue(string columnName, object? newValue) => Value = newValue;
    }
}
