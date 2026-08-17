namespace Anonymyzer.PostgreSql.Tests;

using Anonymyzer.Base.Generation;
using Anonymyzer.Generators.Simple;

public class EmailAddressGeneratorTests
{
    [Fact]
    public async Task OpaquePatternProducesUniqueReservedDomainAddresses()
    {
        var generator = new EmailAddressGenerator();
        GeneratorBinding binding = Bind();
        var configuration = new EmailAddressGeneratorConfiguration
        {
            Pattern = EmailAddressPattern.Opaque,
            Domain = "example.invalid",
            OpaquePrefix = "account",
            StartAt = 7,
            MinimumDigits = 4,
            PreserveNulls = false
        };
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        Assert.Empty(generator.GetDataRequirements(binding, configuration));
        await using IGeneratorSession session = await generator.PrepareAsync(
            new GeneratorPreparationContext(binding, new RejectingDataReader()),
            configuration,
            cancellationToken);
        var first = new FakeGeneratorRow(("email", null));
        var second = new FakeGeneratorRow(("email", "old@example.com"));

        await session.ApplyAsync(first, cancellationToken);
        await session.ApplyAsync(second, cancellationToken);

        Assert.Equal("account.0007@example.invalid", first["email"]);
        Assert.Equal("account.0008@example.invalid", second["email"]);
    }

    [Fact]
    public async Task NameBasedPatternUsesGeneratedNameColumnsAndNormalizesPolishCharacters()
    {
        var generator = new EmailAddressGenerator();
        GeneratorBinding binding = Bind();
        var configuration = new EmailAddressGeneratorConfiguration
        {
            Pattern = EmailAddressPattern.NameBased,
            Domain = "example.invalid",
            FirstNameColumn = "first_name",
            LastNameColumn = "last_name",
            NameValueSource = GeneratorValueSource.Generated,
            StartAt = 12,
            MinimumDigits = 4,
            PreserveNulls = false
        };
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        GeneratorDataRequirement requirement = Assert.Single(generator.GetDataRequirements(binding, configuration));
        Assert.Equal(GeneratorValueSource.Generated, requirement.ValueSource);
        Assert.Equal(["first_name", "last_name"], requirement.Columns);

        await using IGeneratorSession session = await generator.PrepareAsync(
            new GeneratorPreparationContext(binding, new RejectingDataReader()),
            configuration,
            cancellationToken);
        var row = new FakeGeneratorRow(
            ("email", "old@example.com"),
            ("first_name", "Małgorzata"),
            ("last_name", "Żółć"));

        await session.ApplyAsync(row, cancellationToken);

        Assert.Equal("malgorzata.zolc.0012@example.invalid", row["email"]);
    }

    [Fact]
    public async Task PreservedNullDoesNotConsumeSequenceNumber()
    {
        var generator = new EmailAddressGenerator();
        GeneratorBinding binding = Bind();
        var configuration = new EmailAddressGeneratorConfiguration
        {
            PreserveNulls = true,
            StartAt = 3,
            MinimumDigits = 2
        };
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await using IGeneratorSession session = await generator.PrepareAsync(
            new GeneratorPreparationContext(binding, new RejectingDataReader()),
            configuration,
            cancellationToken);
        var empty = new FakeGeneratorRow(("email", null));
        var populated = new FakeGeneratorRow(("email", "old@example.com"));

        await session.ApplyAsync(empty, cancellationToken);
        await session.ApplyAsync(populated, cancellationToken);

        Assert.Null(empty["email"]);
        Assert.Equal("person.03@example.invalid", populated["email"]);
    }

    [Fact]
    public void CodecRejectsInvalidDomainAndIncompleteNamePattern()
    {
        var codec = new EmailAddressGeneratorConfigurationCodec();
        var configuration = new EmailAddressGeneratorConfiguration
        {
            Pattern = EmailAddressPattern.NameBased,
            Domain = "not a domain",
            FirstNameColumn = string.Empty,
            LastNameColumn = string.Empty
        };

        IReadOnlyList<string> errors = codec.Validate(configuration);

        Assert.Contains(errors, error => error.Contains("Domain", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains("FirstNameColumn", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains("LastNameColumn", StringComparison.Ordinal));
    }

    private static GeneratorBinding Bind() => new(
        new GeneratorTableReference("public", "people"),
        new Dictionary<string, string> { [EmailAddressGenerator.ValueOutput] = "email" });

    private sealed class RejectingDataReader : IGeneratorDataReader
    {
        public IAsyncEnumerable<GeneratorDataRow> ReadAsync(
            GeneratorDataRequirement requirement,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("EmailAddress must only read current-row values.");
        }
    }

    private sealed class FakeGeneratorRow(params (string Column, object? Value)[] values) : IGeneratorRow
    {
        private readonly Dictionary<string, object?> _values = values.ToDictionary(
            item => item.Column,
            item => item.Value,
            StringComparer.OrdinalIgnoreCase);

        public object? this[string columnName] => GetValue(columnName);

        public object? GetValue(string columnName) =>
            _values.TryGetValue(columnName, out object? value) ? value : null;

        public void SetValue(string columnName, object? value) => _values[columnName] = value;
    }
}
