namespace Anonymyzer.LanguagePack.English;

using Anonymyzer.Base.LanguagePacks;

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
}
