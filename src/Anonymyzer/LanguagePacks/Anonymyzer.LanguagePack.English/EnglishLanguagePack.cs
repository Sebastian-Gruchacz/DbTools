namespace Anonymyzer.LanguagePack.English;

using Anonymyzer.Base.LanguagePacks;
using Anonymyzer.Generators.Address;
using Anonymyzer.Generators.Person;
using Anonymyzer.Generators.Simple;
using Newtonsoft.Json.Linq;

public sealed class EnglishLanguagePack : ILanguagePack
{
    public LanguagePackDescriptor Descriptor { get; } = new(
        "English",
        "English",
        "1.0.0",
        ["en-US"]);

    public IReadOnlyList<Type> ProviderTypes { get; } =
    [
        typeof(EnglishColumnCandidateRuleProvider),
        typeof(EnglishPostalAddressLocaleDataProvider),
        typeof(EnglishPersonLocaleDataProvider),
        typeof(EnglishPhoneNumberLocaleDataProvider),
        typeof(EnglishNationalIdentifierLocaleDataProvider),
        typeof(EnglishCompanyNameLocaleDataProvider)
    ];

    public IReadOnlyList<LanguagePackProfileDefinition> ProfileDefinitions { get; } =
    [
        Profile("PhoneNumber:en-US:Default", "Phone number (English default)", PhoneNumberGenerator.GeneratorType,
            PhoneNumberGenerator.GeneratorVersion, new PhoneNumberGeneratorConfigurationCodec()),
        Profile("CompanyName:en-US:Default", "Company name (English default)", CompanyNameGenerator.GeneratorType,
            CompanyNameGenerator.GeneratorVersion, new CompanyNameGeneratorConfigurationCodec()),
        Profile("NationalIdentifier:en-US:Default", "National identifier (English default)",
            NationalIdentifierGenerator.GeneratorType, NationalIdentifierGenerator.GeneratorVersion,
            new NationalIdentifierGeneratorConfigurationCodec()),
        Profile("PostalAddress:en-US:Default", "Postal address (English default)", PostalAddressGenerator.GeneratorType,
            PostalAddressGenerator.GeneratorVersion, new PostalAddressGeneratorConfigurationCodec()),
        Profile("PersonIdentity:en-US:Default", "Person identity (English default)", PersonIdentityGenerator.GeneratorType,
            PersonIdentityGenerator.GeneratorVersion, new PersonIdentityGeneratorConfigurationCodec())
    ];

    private static LanguagePackProfileDefinition Profile(
        string id,
        string displayName,
        string generatorType,
        string generatorVersion,
        Anonymyzer.Base.Generation.IGeneratorConfigurationCodec codec)
    {
        JObject options = codec.Serialize(codec.CreateDefault());
        options["Locale"] = "en-US";
        return new LanguagePackProfileDefinition(id, displayName, generatorType, generatorVersion, "en-US", options);
    }
}
