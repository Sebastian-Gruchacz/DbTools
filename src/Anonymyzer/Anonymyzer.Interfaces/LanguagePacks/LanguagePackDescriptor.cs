namespace Anonymyzer.Base.LanguagePacks;

public sealed record LanguagePackDescriptor(
    string Id,
    string DisplayName,
    string Version,
    IReadOnlyList<string> Locales);
