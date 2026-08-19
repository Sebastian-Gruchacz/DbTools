namespace Anonymyzer.Base.LanguagePacks;

public interface ILanguagePack
{
    LanguagePackDescriptor Descriptor { get; }

    IReadOnlyList<Type> ProviderTypes { get; }

    IReadOnlyList<LanguagePackProfileDefinition> ProfileDefinitions { get; }
}
