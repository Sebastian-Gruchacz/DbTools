namespace Anonymyzer.PostgreSql.Tests;

using Anonymyzer.Base.Generation;
using Anonymyzer.Generators.Simple;

public class AccountLoginGeneratorTests
{
    [Fact]
    public async Task BuildsUniqueNormalizedLoginFromGeneratedNames()
    {
        var configuration = new AccountLoginGeneratorConfiguration
        {
            Pattern = AccountLoginPattern.NameBased,
            FirstNameColumn = "first_name",
            LastNameColumn = "last_name",
            NameValueSource = GeneratorValueSource.Generated,
            Separator = ".",
            StartAt = 7,
            MinimumDigits = 4,
            PreserveNulls = false
        };
        var generator = new AccountLoginGenerator();
        GeneratorBinding binding = CreateBinding();
        GeneratorDataRequirement requirement = Assert.Single(generator.GetDataRequirements(binding, configuration));
        Assert.Equal(GeneratorValueSource.Generated, requirement.ValueSource);
        await using IGeneratorSession session = await generator.PrepareAsync(
            new GeneratorPreparationContext(binding, new RejectingDataReader()), configuration,
            TestContext.Current.CancellationToken);
        var row = new DictionaryRow(new Dictionary<string, object?>
        {
            ["login"] = null,
            ["first_name"] = "Łukasz",
            ["last_name"] = "Żółć"
        });

        await session.ApplyAsync(row, TestContext.Current.CancellationToken);

        Assert.Equal("lukasz.zolc.0007", row.GetValue("login"));
    }

    [Fact]
    public async Task OpaquePatternPreservesNullsWithoutConsumingSequence()
    {
        var configuration = new AccountLoginGeneratorConfiguration { OpaquePrefix = "konto", StartAt = 1 };
        await using IGeneratorSession session = await PrepareAsync(configuration);
        var empty = new DictionaryRow(new Dictionary<string, object?> { ["login"] = null });
        var populated = new DictionaryRow(new Dictionary<string, object?> { ["login"] = "old" });

        await session.ApplyAsync(empty, TestContext.Current.CancellationToken);
        await session.ApplyAsync(populated, TestContext.Current.CancellationToken);

        Assert.Null(empty.GetValue("login"));
        Assert.Equal("konto.000001", populated.GetValue("login"));
    }

    [Fact]
    public void CodecRejectsUnsupportedSeparatorAndIncompleteNamePattern()
    {
        var codec = new AccountLoginGeneratorConfigurationCodec();
        var configuration = new AccountLoginGeneratorConfiguration
        {
            Pattern = AccountLoginPattern.NameBased,
            Separator = "/"
        };

        Assert.Equal(3, codec.Validate(configuration).Count);
    }

    private static GeneratorBinding CreateBinding() => new(
        new GeneratorTableReference("public", "people"),
        new Dictionary<string, string> { [AccountLoginGenerator.ValueOutput] = "login" });

    private static async ValueTask<IGeneratorSession> PrepareAsync(AccountLoginGeneratorConfiguration configuration)
    {
        var generator = new AccountLoginGenerator();
        return await generator.PrepareAsync(
            new GeneratorPreparationContext(CreateBinding(), new RejectingDataReader()), configuration,
            TestContext.Current.CancellationToken);
    }

    private sealed class RejectingDataReader : IGeneratorDataReader
    {
        public IAsyncEnumerable<GeneratorDataRow> ReadAsync(
            GeneratorDataRequirement requirement, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("AccountLogin must not scan the database.");
    }

    private sealed class DictionaryRow(Dictionary<string, object?> values) : IGeneratorRow
    {
        public object? GetValue(string columnName) => values.TryGetValue(columnName, out object? value) ? value : null;
        public void SetValue(string columnName, object? value) => values[columnName] = value;
    }
}
