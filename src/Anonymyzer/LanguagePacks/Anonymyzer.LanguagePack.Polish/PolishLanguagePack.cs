namespace Anonymyzer.LanguagePack.Polish;

using Anonymyzer.Base.LanguagePacks;

public sealed class PolishLanguagePack : ILanguagePack
{
    public LanguagePackDescriptor Descriptor { get; } = new(
        "Polish",
        "Polish",
        "1.0.0",
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
}
