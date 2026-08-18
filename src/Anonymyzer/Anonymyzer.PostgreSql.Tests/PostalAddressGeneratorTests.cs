namespace Anonymyzer.PostgreSql.Tests;

using Anonymyzer.Base.Generation;
using Anonymyzer.Generators.Address;
using Anonymyzer.LanguagePack.English;
using Anonymyzer.LanguagePack.Polish;

public class PostalAddressGeneratorTests
{
    [Theory]
    [InlineData("pl-PL")]
    [InlineData("en-US")]
    public async Task GeneratesCoherentLocaleSpecificAddress(string locale)
    {
        var generator = new PostalAddressGenerator(
        [
            new PolishPostalAddressLocaleDataProvider(),
            new EnglishPostalAddressLocaleDataProvider()
        ]);
        GeneratorBinding binding = CreateBinding();
        var configuration = new PostalAddressGeneratorConfiguration { Locale = locale, Seed = 123 };
        var first = new DictionaryRow();
        var repeated = new DictionaryRow();

        await using IGeneratorSession firstSession = await generator.PrepareAsync(
            new GeneratorPreparationContext(binding, new RejectingDataReader()),
            configuration,
            TestContext.Current.CancellationToken);
        await using IGeneratorSession repeatedSession = await generator.PrepareAsync(
            new GeneratorPreparationContext(binding, new RejectingDataReader()),
            configuration,
            TestContext.Current.CancellationToken);
        await firstSession.ApplyAsync(first, TestContext.Current.CancellationToken);
        await repeatedSession.ApplyAsync(repeated, TestContext.Current.CancellationToken);

        Assert.Equal(first.Values, repeated.Values);
        AssertPostalCodeMatchesCity(first);
        Assert.Equal(locale == "pl-PL" ? "Polska" : "United States", first.GetValue("country"));
    }

    [Fact]
    public void DescriptorExposesAllAddressRolesAsOptionalOutputs()
    {
        var generator = new PostalAddressGenerator(Array.Empty<IPostalAddressLocaleDataProvider>());

        Assert.Equal(GeneratorExecutionScope.Row, generator.Descriptor.Scope);
        Assert.Equal(
            ["Address.Country", "Address.Region", "Address.City", "Address.Street", "Address.PostalCode"],
            generator.Descriptor.Outputs.Select(output => output.SemanticRole));
        Assert.All(generator.Descriptor.Outputs, output => Assert.False(output.Required));
    }

    private static GeneratorBinding CreateBinding() => new(
        new GeneratorTableReference("public", "addresses"),
        new Dictionary<string, string>
        {
            [PostalAddressGenerator.CountryOutput] = "country",
            [PostalAddressGenerator.RegionOutput] = "region",
            [PostalAddressGenerator.CityOutput] = "city",
            [PostalAddressGenerator.StreetOutput] = "street",
            [PostalAddressGenerator.PostalCodeOutput] = "postal_code"
        });

    private static void AssertPostalCodeMatchesCity(DictionaryRow row)
    {
        var expected = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Warszawa"] = "00-001",
            ["Kraków"] = "30-001",
            ["Wrocław"] = "50-001",
            ["Poznań"] = "60-001",
            ["Gdańsk"] = "80-001",
            ["Łódź"] = "90-001",
            ["New York"] = "10001",
            ["Los Angeles"] = "90001",
            ["Chicago"] = "60601",
            ["Houston"] = "77002",
            ["Phoenix"] = "85003",
            ["Philadelphia"] = "19103"
        };

        string city = Assert.IsType<string>(row.GetValue("city"));
        Assert.Equal(expected[city], row.GetValue("postal_code"));
    }

    private sealed class RejectingDataReader : IGeneratorDataReader
    {
        public IAsyncEnumerable<GeneratorDataRow> ReadAsync(
            GeneratorDataRequirement requirement,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("PostalAddress must not read source data.");
    }

    private sealed class DictionaryRow : IGeneratorRow
    {
        private readonly Dictionary<string, object?> _values = new(StringComparer.OrdinalIgnoreCase);

        public IReadOnlyDictionary<string, object?> Values => _values;

        public object? GetValue(string columnName) => _values.TryGetValue(columnName, out object? value) ? value : null;

        public void SetValue(string columnName, object? value) => _values[columnName] = value;
    }
}
