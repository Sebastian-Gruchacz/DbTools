namespace Anonymyzer.PostgreSql.Tests;

using Anonymyzer.Base.Detection;
using Anonymyzer.Base.LanguagePacks;
using Anonymyzer.Generators.Person;
using Anonymyzer.LanguagePack.English;
using Anonymyzer.LanguagePack.Polish;

public sealed class LanguagePackCatalogTests
{
    [Fact]
    public void CreatesOnlyProvidersMatchingTheRequestedContract()
    {
        var catalog = new LanguagePackCatalog(
        [
            new EnglishLanguagePack(),
            new PolishLanguagePack()
        ]);

        IReadOnlyList<IColumnCandidateRuleProvider> rules =
            catalog.CreateProviders<IColumnCandidateRuleProvider>();
        IReadOnlyList<IPersonLocaleDataProvider> people =
            catalog.CreateProviders<IPersonLocaleDataProvider>();

        Assert.Equal(2, rules.Count);
        Assert.Equal(["en-US", "pl-PL"], people.Select(provider => provider.Locale).OrderBy(value => value));
    }

    [Fact]
    public void RejectsDuplicatePackIds()
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            new LanguagePackCatalog(
            [
                new EnglishLanguagePack(),
                new DuplicateEnglishPack()
            ]));

        Assert.Contains("Duplicate language pack id 'English'", exception.Message);
    }

    private sealed class DuplicateEnglishPack : ILanguagePack
    {
        public LanguagePackDescriptor Descriptor { get; } = new("English", "Duplicate", "1.0.0", ["en"]);

        public IReadOnlyList<Type> ProviderTypes { get; } = [];
    }
}
