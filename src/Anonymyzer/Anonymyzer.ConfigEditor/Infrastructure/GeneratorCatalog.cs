namespace Anonymyzer.ConfigEditor.Infrastructure;

using Anonymyzer.Base.Generation;
using Anonymyzer.Base.LanguagePacks;
using Anonymyzer.Configuration;
using Anonymyzer.Generators.Address;
using Anonymyzer.Generators.Person;
using Anonymyzer.Generators.Simple;

internal sealed class GeneratorCatalog
{
    private static readonly HashSet<string> LocalizedGeneratorTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "PhoneNumber",
        "CompanyName",
        "TaxIdentifier",
        "BankAccount",
        "NationalIdentifier",
        "PostalAddress",
        "PersonIdentity"
    };

    private readonly IGenerator[] _generators;
    private readonly GeneratorProfileConfiguration[] _languagePackProfiles;
    private readonly HashSet<string> _activeLocales;

    public GeneratorCatalog(LanguagePackCatalog languagePacks)
    {
        ArgumentNullException.ThrowIfNull(languagePacks);
        _activeLocales = languagePacks.Packs
            .SelectMany(pack => pack.Descriptor.Locales)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        _generators =
        [
        new ShufflingTextGenerator(),
        new FixedTextGenerator(),
        new JsonPathRedactorGenerator(),
        new SequentialTextGenerator(),
        new EmailAddressGenerator(),
        new AccountLoginGenerator(),
        new PhoneNumberGenerator(languagePacks.CreateProviders<IPhoneNumberLocaleDataProvider>()),
        new UuidGenerator(),
        new CompanyNameGenerator(languagePacks.CreateProviders<ICompanyNameLocaleDataProvider>()),
        new TaxIdentifierGenerator(languagePacks.CreateProviders<ITaxIdentifierLocaleDataProvider>()),
        new BankAccountGenerator(languagePacks.CreateProviders<IBankAccountLocaleDataProvider>()),
        new BirthDateGenerator(),
        new GenderGenerator(),
        new NationalIdentifierGenerator(languagePacks.CreateProviders<INationalIdentifierLocaleDataProvider>()),
        new PostalAddressGenerator(languagePacks.CreateProviders<IPostalAddressLocaleDataProvider>()),
        new PersonIdentityGenerator(languagePacks.CreateProviders<IPersonLocaleDataProvider>())
        ];
        _languagePackProfiles = languagePacks.Profiles.Select(item => new GeneratorProfileConfiguration
        {
            Id = item.Profile.Id,
            DisplayName = item.Profile.DisplayName,
            GeneratorType = item.Profile.GeneratorType,
            GeneratorVersion = item.Profile.GeneratorVersion,
            Locale = item.Profile.Locale,
            Origin = $"Language pack: {item.Pack.Descriptor.DisplayName} {item.Pack.Descriptor.Version}",
            Options = (Newtonsoft.Json.Linq.JObject)item.Profile.Options.DeepClone()
        }).ToArray();
    }

    public IReadOnlyList<GeneratorDescriptor> Descriptors => _generators.Select(generator => generator.Descriptor).ToArray();

    public IReadOnlyList<GeneratorProfileConfiguration> CreateDefaultProfiles() =>
        _generators
            .Where(generator => !LocalizedGeneratorTypes.Contains(generator.Descriptor.Type))
            .Select(CreateDefaultProfile)
            .Concat(_languagePackProfiles.Select(CloneProfile))
            .ToArray();

    public AnonymizationConfiguration CreateNewConfiguration()
    {
        var configuration = new AnonymizationConfiguration();
        configuration.GeneratorProfiles.AddRange(CreateDefaultProfiles());

        return configuration;
    }

    public IGenerator? Find(string generatorType, string generatorVersion)
    {
        return _generators.FirstOrDefault(generator =>
            generator.Descriptor.Type.Equals(generatorType, StringComparison.OrdinalIgnoreCase)
            && generator.Descriptor.Version.Equals(generatorVersion, StringComparison.Ordinal));
    }

    public IReadOnlyList<string> ValidateProfileLocales(IEnumerable<GeneratorProfileConfiguration> profiles)
    {
        return GeneratorProfileLocaleValidator.Validate(profiles, _activeLocales);
    }

    private static GeneratorProfileConfiguration CreateDefaultProfile(IGenerator generator) => new()
    {
        Id = $"{generator.Descriptor.Type}:Default",
        DisplayName = $"{generator.Descriptor.DisplayName} (Default)",
        GeneratorType = generator.Descriptor.Type,
        GeneratorVersion = generator.Descriptor.Version,
        Locale = string.Empty,
        Origin = "Built-in",
        Options = generator.Configuration.Serialize(generator.Configuration.CreateDefault())
    };

    private static GeneratorProfileConfiguration CloneProfile(GeneratorProfileConfiguration profile) => new()
    {
        Id = profile.Id,
        DisplayName = profile.DisplayName,
        GeneratorType = profile.GeneratorType,
        GeneratorVersion = profile.GeneratorVersion,
        Locale = profile.Locale,
        Origin = profile.Origin,
        Options = (Newtonsoft.Json.Linq.JObject)profile.Options.DeepClone()
    };
}
