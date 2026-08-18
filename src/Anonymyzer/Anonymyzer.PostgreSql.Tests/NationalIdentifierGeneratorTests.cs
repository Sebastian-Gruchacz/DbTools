namespace Anonymyzer.PostgreSql.Tests;

using Anonymyzer.Base.Generation;
using Anonymyzer.Generators.Person;
using Anonymyzer.LanguagePack.Polish;

public class NationalIdentifierGeneratorTests
{
    [Theory]
    [InlineData("1899-12-31", PersonGenderSelection.Female)]
    [InlineData("1900-01-01", PersonGenderSelection.Male)]
    [InlineData("2000-02-29", PersonGenderSelection.Any)]
    [InlineData("2199-06-15", PersonGenderSelection.Female)]
    [InlineData("2299-12-31", PersonGenderSelection.Male)]
    public void PolishProviderEncodesDateGenderAndChecksum(string dateText, PersonGenderSelection selection)
    {
        DateOnly date = DateOnly.Parse(dateText, System.Globalization.CultureInfo.InvariantCulture);
        var provider = new PolishNationalIdentifierLocaleDataProvider();

        GeneratedNationalIdentifier generated = provider.Generate(0, 0, date, date, selection);

        Assert.True(PolishNationalIdentifierLocaleDataProvider.IsValidPesel(generated.Value));
        Assert.True(PolishNationalIdentifierLocaleDataProvider.TryDecodeBirthDate(generated.Value, out DateOnly decoded));
        Assert.Equal(date, decoded);
        if (selection != PersonGenderSelection.Any)
        {
            Assert.Equal(selection.ToString(), generated.Gender.ToString());
        }
    }

    [Fact]
    public async Task GeneratorIsDeterministicUniqueAndPreservesNulls()
    {
        var configuration = new NationalIdentifierGeneratorConfiguration
        {
            MinimumBirthDate = "1980-01-01",
            MaximumBirthDate = "1980-01-03",
            Gender = PersonGenderSelection.Female,
            Seed = 11
        };
        await using IGeneratorSession firstSession = await PrepareAsync(configuration);
        await using IGeneratorSession secondSession = await PrepareAsync(configuration);
        var empty = new FakeGeneratorRow(null);
        var first = new FakeGeneratorRow("old");
        var repeated = new FakeGeneratorRow("old");

        await firstSession.ApplyAsync(empty, TestContext.Current.CancellationToken);
        await firstSession.ApplyAsync(first, TestContext.Current.CancellationToken);
        await secondSession.ApplyAsync(repeated, TestContext.Current.CancellationToken);

        Assert.Null(empty.Value);
        Assert.Equal(first.Value, repeated.Value);
    }

    [Fact]
    public async Task ExhaustsAConfiguredSingleDateAndGenderWithoutDuplicates()
    {
        var configuration = new NationalIdentifierGeneratorConfiguration
        {
            MinimumBirthDate = "2001-01-01",
            MaximumBirthDate = "2001-01-01",
            Gender = PersonGenderSelection.Male,
            PreserveNulls = false
        };
        await using IGeneratorSession session = await PrepareAsync(configuration);
        var values = new HashSet<object?>();
        for (int index = 0; index < 5000; index++)
        {
            var row = new FakeGeneratorRow(null);
            await session.ApplyAsync(row, TestContext.Current.CancellationToken);
            Assert.True(values.Add(row.Value));
        }

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await session.ApplyAsync(new FakeGeneratorRow(null), TestContext.Current.CancellationToken));
    }

    [Fact]
    public void CodecRejectsInvalidDatesAndGender()
    {
        var codec = new NationalIdentifierGeneratorConfigurationCodec();
        var configuration = new NationalIdentifierGeneratorConfiguration
        {
            MinimumBirthDate = "2300-01-01",
            MaximumBirthDate = "not-a-date",
            Gender = (PersonGenderSelection)99
        };

        Assert.Equal(3, codec.Validate(configuration).Count);
    }

    private static async ValueTask<IGeneratorSession> PrepareAsync(
        NationalIdentifierGeneratorConfiguration configuration)
    {
        var generator = new NationalIdentifierGenerator([new PolishNationalIdentifierLocaleDataProvider()]);
        GeneratorBinding binding = new(
            new GeneratorTableReference("public", "people"),
            new Dictionary<string, string> { [NationalIdentifierGenerator.ValueOutput] = "pesel" });
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
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("NationalIdentifier must not read source data.");
    }

    private sealed class FakeGeneratorRow(object? value) : IGeneratorRow
    {
        public object? Value { get; private set; } = value;
        public object? GetValue(string columnName) => Value;
        public void SetValue(string columnName, object? newValue) => Value = newValue;
    }
}
