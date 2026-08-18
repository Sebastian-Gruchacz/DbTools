namespace Anonymyzer.PostgreSql.Tests;

using System.Text.RegularExpressions;
using Anonymyzer.Base.Generation;
using Anonymyzer.Generators.Simple;

public class UuidGeneratorTests
{
    [Theory]
    [InlineData(UuidTextFormat.Hyphenated, @"^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$")]
    [InlineData(UuidTextFormat.Compact, @"^[0-9a-f]{12}4[0-9a-f]{3}[89ab][0-9a-f]{15}$")]
    [InlineData(UuidTextFormat.Braced, @"^\{[0-9a-f-]{36}\}$")]
    [InlineData(UuidTextFormat.Parenthesized, @"^\([0-9a-f-]{36}\)$")]
    public async Task ProducesConfiguredUuidTextFormat(UuidTextFormat format, string expectedPattern)
    {
        var configuration = new UuidGeneratorConfiguration
        {
            Seed = "format-test",
            Format = format,
            PreserveNulls = false
        };
        await using IGeneratorSession session = await PrepareAsync(configuration);
        var row = new FakeGeneratorRow(null);

        await session.ApplyAsync(row, TestContext.Current.CancellationToken);

        Assert.Matches(new Regex(expectedPattern, RegexOptions.CultureInvariant), Assert.IsType<string>(row.Value));
    }

    [Fact]
    public async Task SameSeedAndOrdinalProduceTheSameSequence()
    {
        var configuration = new UuidGeneratorConfiguration { Seed = "repeatable", StartAt = 17 };
        await using IGeneratorSession firstSession = await PrepareAsync(configuration);
        await using IGeneratorSession secondSession = await PrepareAsync(configuration);
        var first = new FakeGeneratorRow("old");
        var second = new FakeGeneratorRow("old");

        await firstSession.ApplyAsync(first, TestContext.Current.CancellationToken);
        await secondSession.ApplyAsync(second, TestContext.Current.CancellationToken);

        Assert.Equal(first.Value, second.Value);
    }

    [Fact]
    public async Task PreservedNullDoesNotConsumeTheSequence()
    {
        var configuration = new UuidGeneratorConfiguration { Seed = "null-test", PreserveNulls = true };
        await using IGeneratorSession withNull = await PrepareAsync(configuration);
        await using IGeneratorSession withoutNull = await PrepareAsync(configuration);
        var empty = new FakeGeneratorRow(null);
        var afterNull = new FakeGeneratorRow("old");
        var direct = new FakeGeneratorRow("old");

        await withNull.ApplyAsync(empty, TestContext.Current.CancellationToken);
        await withNull.ApplyAsync(afterNull, TestContext.Current.CancellationToken);
        await withoutNull.ApplyAsync(direct, TestContext.Current.CancellationToken);

        Assert.Null(empty.Value);
        Assert.Equal(direct.Value, afterNull.Value);
    }

    [Fact]
    public async Task SupportsUppercaseAndRejectsSequenceOverflow()
    {
        var configuration = new UuidGeneratorConfiguration
        {
            Seed = "last-value",
            StartAt = long.MaxValue,
            Uppercase = true,
            PreserveNulls = false
        };
        await using IGeneratorSession session = await PrepareAsync(configuration);
        var row = new FakeGeneratorRow(null);

        await session.ApplyAsync(row, TestContext.Current.CancellationToken);
        string value = Assert.IsType<string>(row.Value);

        Assert.Equal(value.ToUpperInvariant(), value);
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await session.ApplyAsync(new FakeGeneratorRow(null), TestContext.Current.CancellationToken));
    }

    [Fact]
    public void CodecRejectsInvalidConfiguration()
    {
        var codec = new UuidGeneratorConfigurationCodec();
        var configuration = new UuidGeneratorConfiguration
        {
            Seed = string.Empty,
            StartAt = -1,
            Format = (UuidTextFormat)999
        };

        Assert.Equal(3, codec.Validate(configuration).Count);
    }

    private static async ValueTask<IGeneratorSession> PrepareAsync(UuidGeneratorConfiguration configuration)
    {
        var generator = new UuidGenerator();
        GeneratorBinding binding = new(
            new GeneratorTableReference("public", "records"),
            new Dictionary<string, string> { [UuidGenerator.ValueOutput] = "external_id" });
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
            throw new InvalidOperationException("Uuid must not read source data.");
        }
    }

    private sealed class FakeGeneratorRow(object? value) : IGeneratorRow
    {
        public object? Value { get; private set; } = value;

        public object? GetValue(string columnName) => Value;

        public void SetValue(string columnName, object? newValue) => Value = newValue;
    }
}
