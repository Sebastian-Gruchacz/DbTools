namespace Anonymyzer.PostgreSql.Tests;

using Anonymyzer.Base.Generation;
using Anonymyzer.Generators.Simple;
using Newtonsoft.Json.Linq;

public class SimpleRowGeneratorTests
{
    [Fact]
    public async Task FixedTextReplacesValuesAndPreservesNulls()
    {
        var generator = new FixedTextGenerator();
        GeneratorBinding binding = Bind(FixedTextGenerator.ValueOutput);
        var configuration = new FixedTextGeneratorConfiguration
        {
            Value = "REDACTED",
            PreserveNulls = true
        };
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        Assert.Empty(generator.GetDataRequirements(binding, configuration));
        await using IGeneratorSession session = await generator.PrepareAsync(
            new GeneratorPreparationContext(binding, new RejectingDataReader()),
            configuration,
            cancellationToken);
        var populated = new FakeGeneratorRow("original");
        var empty = new FakeGeneratorRow(null);

        await session.ApplyAsync(populated, cancellationToken);
        await session.ApplyAsync(empty, cancellationToken);

        Assert.Equal("REDACTED", populated.Value);
        Assert.Null(empty.Value);
    }

    [Fact]
    public async Task FixedTextCanReplaceNullWithAnEmptyString()
    {
        var generator = new FixedTextGenerator();
        GeneratorBinding binding = Bind(FixedTextGenerator.ValueOutput);
        var configuration = new FixedTextGeneratorConfiguration
        {
            Value = string.Empty,
            PreserveNulls = false
        };

        await using IGeneratorSession session = await generator.PrepareAsync(
            new GeneratorPreparationContext(binding, new RejectingDataReader()),
            configuration,
            TestContext.Current.CancellationToken);
        var row = new FakeGeneratorRow(null);

        await session.ApplyAsync(row, TestContext.Current.CancellationToken);

        Assert.Equal(string.Empty, row.Value);
    }

    [Fact]
    public async Task SequentialTextProducesDenseUniqueValuesAroundPreservedNulls()
    {
        var generator = new SequentialTextGenerator();
        GeneratorBinding binding = Bind(SequentialTextGenerator.ValueOutput);
        var configuration = new SequentialTextGeneratorConfiguration
        {
            Prefix = "person-",
            Suffix = "@example.invalid",
            StartAt = 7,
            MinimumDigits = 3,
            PreserveNulls = true
        };
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        Assert.Empty(generator.GetDataRequirements(binding, configuration));
        await using IGeneratorSession session = await generator.PrepareAsync(
            new GeneratorPreparationContext(binding, new RejectingDataReader()),
            configuration,
            cancellationToken);
        var first = new FakeGeneratorRow("first");
        var empty = new FakeGeneratorRow(null);
        var second = new FakeGeneratorRow("second");

        await session.ApplyAsync(first, cancellationToken);
        await session.ApplyAsync(empty, cancellationToken);
        await session.ApplyAsync(second, cancellationToken);

        Assert.Equal("person-007@example.invalid", first.Value);
        Assert.Null(empty.Value);
        Assert.Equal("person-008@example.invalid", second.Value);
    }

    [Fact]
    public void ConfigurationCodecsOwnDefaultsRoundTripsAndValidation()
    {
        var fixedCodec = new FixedTextGeneratorConfigurationCodec();
        var sequentialCodec = new SequentialTextGeneratorConfigurationCodec();

        var fixedDefault = (FixedTextGeneratorConfiguration)fixedCodec.CreateDefault();
        JObject fixedJson = fixedCodec.Serialize(fixedDefault);
        var fixedRestored = (FixedTextGeneratorConfiguration)fixedCodec.Deserialize(fixedJson);
        var invalidSequence = new SequentialTextGeneratorConfiguration
        {
            StartAt = -1,
            MinimumDigits = 0
        };

        Assert.Equal("REDACTED", fixedRestored.Value);
        Assert.True(fixedRestored.PreserveNulls);
        Assert.Equal(2, sequentialCodec.Validate(invalidSequence).Count);
    }

    private static GeneratorBinding Bind(string outputName) => new(
        new GeneratorTableReference("public", "people"),
        new Dictionary<string, string> { [outputName] = "value" });

    private sealed class RejectingDataReader : IGeneratorDataReader
    {
        public IAsyncEnumerable<GeneratorDataRow> ReadAsync(
            GeneratorDataRequirement requirement,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Simple row generators must not read source data.");
        }
    }

    private sealed class FakeGeneratorRow(object? value) : IGeneratorRow
    {
        public object? Value { get; private set; } = value;

        public object? GetValue(string columnName) => Value;

        public void SetValue(string columnName, object? newValue) => Value = newValue;
    }
}
