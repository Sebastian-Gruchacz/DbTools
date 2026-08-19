namespace Anonymyzer.PostgreSql.Tests;

using Anonymyzer.Base.Generation;
using Anonymyzer.Generators.Person;

public class GenderGeneratorTests
{
    [Theory]
    [InlineData(100, "K")]
    [InlineData(0, "M")]
    public async Task HonorsConfiguredBoundaryPercentageAndValues(int femalePercentage, string expected)
    {
        var configuration = new GenderGeneratorConfiguration
        {
            FemaleValue = "K",
            MaleValue = "M",
            FemalePercentage = femalePercentage,
            PreserveNulls = false,
            Seed = 17
        };
        await using IGeneratorSession session = await PrepareAsync(configuration);

        for (int index = 0; index < 100; index++)
        {
            var row = new FakeRow(null);
            await session.ApplyAsync(row, TestContext.Current.CancellationToken);
            Assert.Equal(expected, row.Value);
        }
    }

    [Fact]
    public async Task IsDeterministicAndPreservesNulls()
    {
        var configuration = new GenderGeneratorConfiguration { Seed = 123 };
        await using IGeneratorSession firstSession = await PrepareAsync(configuration);
        await using IGeneratorSession repeatedSession = await PrepareAsync(configuration);
        var nullRow = new FakeRow(null);
        var first = new FakeRow("old");
        var repeated = new FakeRow("old");

        await firstSession.ApplyAsync(nullRow, TestContext.Current.CancellationToken);
        await firstSession.ApplyAsync(first, TestContext.Current.CancellationToken);
        await repeatedSession.ApplyAsync(repeated, TestContext.Current.CancellationToken);

        Assert.Null(nullRow.Value);
        Assert.Equal(first.Value, repeated.Value);
    }

    [Fact]
    public void CodecRejectsInvalidValuesAndPercentage()
    {
        var codec = new GenderGeneratorConfigurationCodec();
        var configuration = new GenderGeneratorConfiguration
        {
            FemaleValue = "same",
            MaleValue = "SAME",
            FemalePercentage = 101
        };

        Assert.Equal(2, codec.Validate(configuration).Count);
    }

    private static async ValueTask<IGeneratorSession> PrepareAsync(GenderGeneratorConfiguration configuration)
    {
        var generator = new GenderGenerator();
        GeneratorBinding binding = new(
            new GeneratorTableReference("public", "people"),
            new Dictionary<string, string> { [GenderGenerator.ValueOutput] = "gender" });
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
            throw new InvalidOperationException("Gender must not read source data.");
    }

    private sealed class FakeRow(object? value) : IGeneratorRow
    {
        public object? Value { get; private set; } = value;

        public object? GetValue(string columnName) => Value;

        public void SetValue(string columnName, object? newValue) => Value = newValue;
    }
}
