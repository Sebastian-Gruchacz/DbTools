namespace Anonymyzer.PostgreSql.Tests;

using Anonymyzer.Base.Generation;
using Anonymyzer.Generators.Person;
using Anonymyzer.LanguagePack.English;
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
    public void EnglishProviderUsesOnlySafeUnassignedSsnPrefix()
    {
        var provider = new EnglishNationalIdentifierLocaleDataProvider();
        var minimum = new DateOnly(1980, 1, 1);
        var maximum = new DateOnly(2000, 12, 31);

        GeneratedNationalIdentifier first = provider.Generate(0, 0, minimum, maximum, PersonGenderSelection.Any);
        GeneratedNationalIdentifier last = provider.Generate(
            EnglishNationalIdentifierLocaleDataProvider.SafeValueCapacity - 1,
            0,
            minimum,
            maximum,
            PersonGenderSelection.Any);

        Assert.Equal(EnglishNationalIdentifierLocaleDataProvider.SafeValueCapacity,
            provider.GetCapacity(minimum, maximum, PersonGenderSelection.Any));
        Assert.Equal("000-00-0000", first.Value);
        Assert.Equal("000-99-9999", last.Value);
        Assert.Matches(@"^000-\d{2}-\d{4}$", first.Value);
        Assert.Matches(@"^000-\d{2}-\d{4}$", last.Value);
    }

    [Fact]
    public async Task EnglishProviderDoesNotRestartSequenceForDifferentDemographicValues()
    {
        var configuration = new NationalIdentifierGeneratorConfiguration
        {
            Locale = "en-US",
            BirthDateColumn = "birth_date",
            GenderColumn = "gender",
            PreserveNulls = false
        };
        var generator = new NationalIdentifierGenerator([new EnglishNationalIdentifierLocaleDataProvider()]);
        GeneratorBinding binding = new(
            new GeneratorTableReference("public", "people"),
            new Dictionary<string, string> { [NationalIdentifierGenerator.ValueOutput] = "ssn" });
        await using IGeneratorSession session = await generator.PrepareAsync(
            new GeneratorPreparationContext(binding, new RejectingDataReader()),
            configuration,
            TestContext.Current.CancellationToken);
        var first = new DictionaryRow(new Dictionary<string, object?>
        {
            ["ssn"] = null,
            ["birth_date"] = new DateOnly(1980, 1, 1),
            ["gender"] = "F"
        });
        var second = new DictionaryRow(new Dictionary<string, object?>
        {
            ["ssn"] = null,
            ["birth_date"] = new DateOnly(1990, 2, 2),
            ["gender"] = "M"
        });

        await session.ApplyAsync(first, TestContext.Current.CancellationToken);
        await session.ApplyAsync(second, TestContext.Current.CancellationToken);

        Assert.NotEqual(first.GetValue("ssn"), second.GetValue("ssn"));
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

    [Fact]
    public async Task UsesConfiguredBirthDateAndGenderColumns()
    {
        var configuration = new NationalIdentifierGeneratorConfiguration
        {
            BirthDateColumn = "birth_date",
            BirthDateValueSource = GeneratorValueSource.Generated,
            GenderColumn = "gender",
            GenderValueSource = GeneratorValueSource.Original
        };
        var generator = new NationalIdentifierGenerator([new PolishNationalIdentifierLocaleDataProvider()]);
        GeneratorBinding binding = new(
            new GeneratorTableReference("public", "people"),
            new Dictionary<string, string> { [NationalIdentifierGenerator.ValueOutput] = "pesel" });
        IReadOnlyList<GeneratorDataRequirement> requirements = generator.GetDataRequirements(binding, configuration);
        Assert.Contains(requirements, item => item.Alias == "birth-date" && item.ValueSource == GeneratorValueSource.Generated);
        Assert.Contains(requirements, item => item.Alias == "gender" && item.ValueSource == GeneratorValueSource.Original);
        await using IGeneratorSession session = await generator.PrepareAsync(
            new GeneratorPreparationContext(binding, new RejectingDataReader()),
            configuration,
            TestContext.Current.CancellationToken);
        var row = new DictionaryRow(new Dictionary<string, object?>
        {
            ["pesel"] = "old",
            ["birth_date"] = new DateOnly(1992, 7, 18),
            ["gender"] = "K"
        });

        await session.ApplyAsync(row, TestContext.Current.CancellationToken);

        string pesel = Assert.IsType<string>(row.GetValue("pesel"));
        Assert.True(PolishNationalIdentifierLocaleDataProvider.TryDecodeBirthDate(pesel, out DateOnly birthDate));
        Assert.Equal(new DateOnly(1992, 7, 18), birthDate);
        Assert.Equal(0, (pesel[9] - '0') % 2);
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

    private sealed class DictionaryRow(Dictionary<string, object?> values) : IGeneratorRow
    {
        public object? GetValue(string columnName) => values.TryGetValue(columnName, out object? value) ? value : null;
        public void SetValue(string columnName, object? value) => values[columnName] = value;
    }
}
