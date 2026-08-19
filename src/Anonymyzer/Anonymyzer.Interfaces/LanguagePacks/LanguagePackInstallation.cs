namespace Anonymyzer.Base.LanguagePacks;

public sealed record LanguagePackInstallation(
    ILanguagePack Pack,
    string Origin,
    string? AssemblyPath,
    bool IsEnabled);
