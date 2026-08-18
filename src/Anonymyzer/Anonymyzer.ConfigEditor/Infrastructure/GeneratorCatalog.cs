namespace Anonymyzer.ConfigEditor.Infrastructure;

using Anonymyzer.Base.Generation;
using Anonymyzer.Configuration;
using Anonymyzer.Generators.Address;
using Anonymyzer.Generators.Person;
using Anonymyzer.Generators.Simple;
using Anonymyzer.LanguagePack.English;
using Anonymyzer.LanguagePack.Polish;

internal sealed class GeneratorCatalog
{
    private readonly IGenerator[] _generators =
    {
        new ShufflingTextGenerator(),
        new FixedTextGenerator(),
        new SequentialTextGenerator(),
        new EmailAddressGenerator(),
        new AccountLoginGenerator(),
        new PhoneNumberGenerator(
        [
            new PolishPhoneNumberLocaleDataProvider(),
            new EnglishPhoneNumberLocaleDataProvider()
        ]),
        new UuidGenerator(),
        new CompanyNameGenerator(
        [
            new PolishCompanyNameLocaleDataProvider(),
            new EnglishCompanyNameLocaleDataProvider()
        ]),
        new TaxIdentifierGenerator([new PolishTaxIdentifierLocaleDataProvider()]),
        new BirthDateGenerator(),
        new GenderGenerator(),
        new NationalIdentifierGenerator(
        [
            new PolishNationalIdentifierLocaleDataProvider(),
            new EnglishNationalIdentifierLocaleDataProvider()
        ]),
        new PostalAddressGenerator(
        [
            new PolishPostalAddressLocaleDataProvider(),
            new EnglishPostalAddressLocaleDataProvider()
        ]),
        new PersonIdentityGenerator(
        [
            new PolishPersonLocaleDataProvider(),
            new EnglishPersonLocaleDataProvider()
        ])
    };

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
            or "NationalIdentifier" or "PostalAddress"
            ? "pl-PL"
            : string.Empty,
        Options = generator.Configuration.Serialize(generator.Configuration.CreateDefault())
    };
}
