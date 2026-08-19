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

    [Fact]
    public void ExposesDistinctLocaleSpecificProfiles()
    {
        var catalog = new LanguagePackCatalog(
        [
            new EnglishLanguagePack(),
            new PolishLanguagePack()
        ]);

        var personProfiles = catalog.Profiles
            .Where(item => item.Profile.GeneratorType == "PersonIdentity")
            .Select(item => item.Profile)
            .ToArray();

        Assert.Equal(2, personProfiles.Length);
        Assert.Equal(2, personProfiles.Select(profile => profile.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Equal(["en-US", "pl-PL"], personProfiles.Select(profile => profile.Locale).OrderBy(value => value));
        Assert.All(personProfiles, profile => Assert.Equal(profile.Locale, profile.Options.Value<string>("Locale")));
    }

    [Fact]
    public void InstallsAndPersistsDisabledState()
    {
        string directory = Path.Combine(Path.GetTempPath(), "anonymyzer-language-pack-tests", Guid.NewGuid().ToString("N"));
        try
        {
            var service = new LanguagePackInstallationService([], directory);

            LanguagePackInstallation installed = service.Install(typeof(EnglishLanguagePack).Assembly.Location);
            bool changed = service.SetEnabled(installed.Pack.Descriptor.Id, enabled: false);
            var reloaded = new LanguagePackInstallationService([], directory);

            Assert.True(changed);
            Assert.Single(reloaded.Installations);
            Assert.False(reloaded.Installations[0].IsEnabled);
            Assert.Empty(reloaded.ActivePacks);
            Assert.Empty(reloaded.LoadWarnings);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    private sealed class DuplicateEnglishPack : ILanguagePack
    {
        public LanguagePackDescriptor Descriptor { get; } = new("English", "Duplicate", "1.0.0", ["en"]);

        public IReadOnlyList<Type> ProviderTypes { get; } = [];

        public IReadOnlyList<LanguagePackProfileDefinition> ProfileDefinitions { get; } = [];
    }
}
