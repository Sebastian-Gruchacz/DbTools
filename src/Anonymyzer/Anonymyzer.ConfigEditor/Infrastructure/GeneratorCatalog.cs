namespace Anonymyzer.ConfigEditor.Infrastructure;

using Anonymyzer.Base.Generation;
using Anonymyzer.Base.LanguagePacks;
using Anonymyzer.Configuration;
using Anonymyzer.Generators.Address;
using Anonymyzer.Generators.Person;
using Anonymyzer.Generators.Simple;

internal sealed class GeneratorCatalog
{
    private readonly IGenerator[] _generators;

    public GeneratorCatalog(LanguagePackCatalog languagePacks)
    {
        ArgumentNullException.ThrowIfNull(languagePacks);
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
    }

    public IReadOnlyList<GeneratorDescriptor> Descriptors => _generators.Select(generator => generator.Descriptor).ToArray();

    public IReadOnlyList<GeneratorProfileConfiguration> CreateDefaultProfiles() =>
        _generators.Select(CreateDefaultProfile).ToArray();

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

    private static GeneratorProfileConfiguration CreateDefaultProfile(IGenerator generator) => new()
    {
        Id = $"{generator.Descriptor.Type}:Default",
        DisplayName = $"{generator.Descriptor.DisplayName} (Default)",
        GeneratorType = generator.Descriptor.Type,
        GeneratorVersion = generator.Descriptor.Version,
        Locale = generator.Descriptor.Type is "PersonIdentity" or "PhoneNumber" or "CompanyName" or "TaxIdentifier"
            or "BankAccount"
            or "NationalIdentifier" or "PostalAddress"
            ? "pl-PL"
            : string.Empty,
        Origin = "Built-in",
        Options = generator.Configuration.Serialize(generator.Configuration.CreateDefault())
    };
}
