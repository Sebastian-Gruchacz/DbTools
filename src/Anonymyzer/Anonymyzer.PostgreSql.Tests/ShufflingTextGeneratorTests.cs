namespace Anonymyzer.PostgreSql.Tests;

using System.Runtime.CompilerServices;
using Anonymyzer.Base.Generation;
using Anonymyzer.Generators.Simple;
using Newtonsoft.Json.Linq;

public class ShufflingTextGeneratorTests
{
    [Fact]
    public async Task ColumnSessionPreservesExactValueDistributionAndNullPositions()
    {
        object?[] sourceValues = { "Alice", "Alice", "Bob", null, "Carol" };
        var binding = new GeneratorBinding(
            new GeneratorTableReference("public", "people"),
            new Dictionary<string, string> { [ShufflingTextGenerator.ValueOutput] = "name" });
        var reader = new FakeDataReader("name", sourceValues);
        var generator = new ShufflingTextGenerator();
        var configuration = new ShufflingTextGeneratorConfiguration
        {
            Seed = 123,
            MinimumPopulation = 2,
            PreserveNulls = true
        };
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await using IGeneratorSession session = await generator.PrepareAsync(
            new GeneratorPreparationContext(binding, reader),
            configuration,
            cancellationToken);

        var outputRows = sourceValues.Select(value => new FakeGeneratorRow("name", value)).ToArray();
        foreach (FakeGeneratorRow row in outputRows)
        {
            await session.ApplyAsync(row, cancellationToken);
        }

        string[] originalNonNull = sourceValues.OfType<string>().OrderBy(value => value).ToArray();
        string[] shuffledNonNull = outputRows.Select(row => row.GetValue("name")).OfType<string>().OrderBy(value => value).ToArray();
        Assert.Equal(originalNonNull, shuffledNonNull);
        Assert.Null(outputRows[3].GetValue("name"));
        Assert.True(reader.Requirement?.RequiresCompleteScan);
        Assert.Equal(GeneratorValueSource.Original, reader.Requirement?.ValueSource);
    }

    [Fact]
    public void ConfigurationCodecOwnsJsonRoundTripAndValidation()
    {
        var codec = new ShufflingTextGeneratorConfigurationCodec();
        var configuration = new ShufflingTextGeneratorConfiguration
        {
            Seed = 42,
            MinimumPopulation = 1,
            PreserveNulls = false,
            MaximumInMemoryBytes = 32 * 1024 * 1024,
            OverflowStrategy = ShuffleOverflowStrategy.EncryptedTemporaryFiles
        };

        JObject json = codec.Serialize(configuration);
        var restored = (ShufflingTextGeneratorConfiguration)codec.Deserialize(json);

        Assert.Equal(42, restored.Seed);
        Assert.False(restored.PreserveNulls);
        Assert.Equal(32 * 1024 * 1024, restored.MaximumInMemoryBytes);
        Assert.Equal(ShuffleOverflowStrategy.EncryptedTemporaryFiles, restored.OverflowStrategy);
        Assert.Contains(codec.Validate(restored), error => error.Contains("at least 2", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RefusesPopulationThatExceedsConfiguredMemoryLimit()
    {
        string sensitiveValue = new('X', 600_000);
        var binding = CreateBinding();
        var generator = new ShufflingTextGenerator();

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await generator.PrepareAsync(
                new GeneratorPreparationContext(binding, new FakeDataReader("name", [sensitiveValue, "other"])),
                new ShufflingTextGeneratorConfiguration
                {
                    MaximumInMemoryBytes = 1024 * 1024,
                    OverflowStrategy = ShuffleOverflowStrategy.Fail
                },
                TestContext.Current.CancellationToken));

        Assert.Contains("memory limit", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(sensitiveValue, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SpillsEncryptedPopulationAndDeletesTemporaryFileOnDispose()
    {
        string first = "FIRST-SECRET-" + new string('A', 600_000);
        string second = "SECOND-SECRET-" + new string('B', 600_000);
        object?[] sourceValues = [first, null, second];
        string temporaryDirectory = Path.Combine(Path.GetTempPath(), "Anonymyzer");
        var existingFiles = Directory.Exists(temporaryDirectory)
            ? Directory.GetFiles(temporaryDirectory, "shuffle-*.bin").ToHashSet(StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var generator = new ShufflingTextGenerator();
        IGeneratorSession session = await generator.PrepareAsync(
            new GeneratorPreparationContext(CreateBinding(), new FakeDataReader("name", sourceValues)),
            new ShufflingTextGeneratorConfiguration
            {
                Seed = 17,
                MinimumPopulation = 2,
                PreserveNulls = true,
                MaximumInMemoryBytes = 1024 * 1024,
                OverflowStrategy = ShuffleOverflowStrategy.EncryptedTemporaryFiles
            },
            TestContext.Current.CancellationToken);
        string[] newFiles = Directory.GetFiles(temporaryDirectory, "shuffle-*.bin")
            .Where(path => !existingFiles.Contains(path))
            .ToArray();

        try
        {
            string marker = "FIRST-SECRET-";
            Assert.NotEmpty(newFiles);
            Assert.All(newFiles, path => Assert.False(ContainsBytes(
                File.ReadAllBytes(path),
                System.Text.Encoding.UTF8.GetBytes(marker))));

            var outputRows = sourceValues.Select(value => new FakeGeneratorRow("name", value)).ToArray();
            foreach (FakeGeneratorRow row in outputRows)
            {
                await session.ApplyAsync(row, TestContext.Current.CancellationToken);
            }

            Assert.Equal(
                [first, second],
                outputRows.Select(row => row.GetValue("name")).OfType<string>().Order().ToArray());
            Assert.Null(outputRows[1].GetValue("name"));
        }
        finally
        {
            await session.DisposeAsync();
        }

        Assert.All(newFiles, path => Assert.False(File.Exists(path)));
    }

    private static GeneratorBinding CreateBinding() => new(
        new GeneratorTableReference("public", "people"),
        new Dictionary<string, string> { [ShufflingTextGenerator.ValueOutput] = "name" });

    private static bool ContainsBytes(byte[] value, byte[] pattern)
    {
        for (int index = 0; index <= value.Length - pattern.Length; index++)
        {
            if (value.AsSpan(index, pattern.Length).SequenceEqual(pattern))
            {
                return true;
            }
        }

        return false;
    }

    private sealed class FakeDataReader(string columnName, IEnumerable<object?> values) : IGeneratorDataReader
    {
        public GeneratorDataRequirement? Requirement { get; private set; }

        public async IAsyncEnumerable<GeneratorDataRow> ReadAsync(
            GeneratorDataRequirement requirement,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            Requirement = requirement;
            foreach (object? value in values)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return new GeneratorDataRow(new Dictionary<string, object?> { [columnName] = value });
                await Task.Yield();
            }
        }
    }

    private sealed class FakeGeneratorRow(string columnName, object? value) : IGeneratorRow
    {
        private readonly Dictionary<string, object?> _values = new() { [columnName] = value };

        public object? GetValue(string requestedColumnName) => _values[requestedColumnName];

        public void SetValue(string requestedColumnName, object? newValue) => _values[requestedColumnName] = newValue;
    }
}
