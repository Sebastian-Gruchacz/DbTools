namespace Anonymyzer.LanguagePack.Polish;

using Anonymyzer.Base.LanguagePacks;
using Anonymyzer.Generators.Address;
using Anonymyzer.Generators.Person;
using Anonymyzer.Generators.Simple;
using Newtonsoft.Json.Linq;

public sealed class PolishLanguagePack : ILanguagePack
{
    public LanguagePackDescriptor Descriptor { get; } = new(
        "Polish",
        "Polish",
        "1.1.0",
        ["pl-PL"]);

    public IReadOnlyList<Type> ProviderTypes { get; } =
    [
        typeof(PolishColumnCandidateRuleProvider),
        typeof(PolishPersonLocaleDataProvider),
        typeof(PolishPostalAddressLocaleDataProvider),
        typeof(PolishPhoneNumberLocaleDataProvider),
        typeof(PolishTaxIdentifierLocaleDataProvider),
        typeof(PolishNationalIdentifierLocaleDataProvider),
        typeof(PolishCompanyNameLocaleDataProvider),
        typeof(PolishBankAccountLocaleDataProvider)
    ];

    public IReadOnlyList<LanguagePackProfileDefinition> ProfileDefinitions { get; } =
    [
        Profile("PhoneNumber:Default", "Phone number (Default)", PhoneNumberGenerator.GeneratorType,
            PhoneNumberGenerator.GeneratorVersion, new PhoneNumberGeneratorConfigurationCodec()),
        Profile("CompanyName:Default", "Company name (Default)", CompanyNameGenerator.GeneratorType,
            CompanyNameGenerator.GeneratorVersion, new CompanyNameGeneratorConfigurationCodec()),
        Profile("TaxIdentifier:Default", "Tax identifier (Default)", TaxIdentifierGenerator.GeneratorType,
            TaxIdentifierGenerator.GeneratorVersion, new TaxIdentifierGeneratorConfigurationCodec()),
        Profile("BankAccount:Default", "Bank account (Default)", BankAccountGenerator.GeneratorType,
            BankAccountGenerator.GeneratorVersion, new BankAccountGeneratorConfigurationCodec()),
        Profile("NationalIdentifier:Default", "National identifier (Default)", NationalIdentifierGenerator.GeneratorType,
            NationalIdentifierGenerator.GeneratorVersion, new NationalIdentifierGeneratorConfigurationCodec()),
        Profile("PostalAddress:Default", "Postal address (Default)", PostalAddressGenerator.GeneratorType,
            PostalAddressGenerator.GeneratorVersion, new PostalAddressGeneratorConfigurationCodec()),
        Profile("PersonIdentity:Default", "Person identity (Default)", PersonIdentityGenerator.GeneratorType,
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
        options["Locale"] = "pl-PL";
        return new LanguagePackProfileDefinition(id, displayName, generatorType, generatorVersion, "pl-PL", options);
    }
}
