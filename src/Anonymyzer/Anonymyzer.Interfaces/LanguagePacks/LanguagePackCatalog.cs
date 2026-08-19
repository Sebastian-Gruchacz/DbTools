namespace Anonymyzer.Base.LanguagePacks;

public sealed class LanguagePackCatalog
{
    private readonly IReadOnlyList<ILanguagePack> _packs;

    public LanguagePackCatalog(IEnumerable<ILanguagePack> packs)
    {
        ArgumentNullException.ThrowIfNull(packs);
        _packs = packs.ToArray();
        Validate(_packs);
    }

    public IReadOnlyList<ILanguagePack> Packs => _packs;

    public IReadOnlyList<(ILanguagePack Pack, LanguagePackProfileDefinition Profile)> Profiles => _packs
        .SelectMany(pack => pack.ProfileDefinitions.Select(profile => (pack, profile)))
        .ToArray();

    public IReadOnlyList<TProvider> CreateProviders<TProvider>() where TProvider : class =>
        _packs
            .SelectMany(pack => pack.ProviderTypes.Select(type => CreateProvider<TProvider>(pack, type)))
            .Where(provider => provider is not null)
            .Cast<TProvider>()
            .ToArray();

    private static TProvider? CreateProvider<TProvider>(ILanguagePack pack, Type providerType)
        where TProvider : class
    {
        if (!typeof(TProvider).IsAssignableFrom(providerType))
        {
            return null;
        }

        try
        {
            return Activator.CreateInstance(providerType) as TProvider
                   ?? throw new InvalidOperationException("Provider instance has an incompatible type.");
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException(
                $"Language pack '{pack.Descriptor.Id}' could not create provider '{providerType.FullName}'. " +
                "Providers must be public concrete classes with parameterless constructors.",
                exception);
        }
    }

    private static void Validate(IReadOnlyList<ILanguagePack> packs)
    {
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (ILanguagePack pack in packs)
        {
            ArgumentNullException.ThrowIfNull(pack);
            LanguagePackDescriptor descriptor = pack.Descriptor
                ?? throw new InvalidOperationException("A language pack descriptor is required.");
            if (string.IsNullOrWhiteSpace(descriptor.Id)
                || string.IsNullOrWhiteSpace(descriptor.DisplayName)
                || string.IsNullOrWhiteSpace(descriptor.Version)
                || descriptor.Locales is null
                || pack.ProviderTypes is null
                || pack.ProfileDefinitions is null)
            {
                throw new InvalidOperationException("Language pack id, display name and version are required.");
            }

            if (!ids.Add(descriptor.Id))
            {
                throw new InvalidOperationException($"Duplicate language pack id '{descriptor.Id}'.");
            }

            if (pack.ProviderTypes.Any(type => type is null || type.IsAbstract || type.IsInterface))
            {
                throw new InvalidOperationException($"Language pack '{descriptor.Id}' contains an invalid provider type.");
            }

            foreach (LanguagePackProfileDefinition profile in pack.ProfileDefinitions)
            {
                if (string.IsNullOrWhiteSpace(profile.Id)
                    || string.IsNullOrWhiteSpace(profile.GeneratorType)
                    || string.IsNullOrWhiteSpace(profile.GeneratorVersion)
                    || string.IsNullOrWhiteSpace(profile.Locale)
                    || !descriptor.Locales.Contains(profile.Locale, StringComparer.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        $"Language pack '{descriptor.Id}' contains invalid profile '{profile.Id}'.");
                }
            }
        }


        string? duplicateProfileId = packs
            .SelectMany(pack => pack.ProfileDefinitions)
            .GroupBy(profile => profile.Id, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1)?.Key;
        if (duplicateProfileId is not null)
        {
            throw new InvalidOperationException($"Duplicate language-pack profile id '{duplicateProfileId}'.");
        }
    }
}
