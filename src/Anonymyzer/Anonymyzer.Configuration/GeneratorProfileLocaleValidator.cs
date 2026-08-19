namespace Anonymyzer.Configuration;

public static class GeneratorProfileLocaleValidator
{
    public static IReadOnlyList<string> Validate(
        IEnumerable<GeneratorProfileConfiguration> profiles,
        IEnumerable<string> activeLocales)
    {
        ArgumentNullException.ThrowIfNull(profiles);
        ArgumentNullException.ThrowIfNull(activeLocales);
        HashSet<string> available = activeLocales.ToHashSet(StringComparer.OrdinalIgnoreCase);
        return profiles
            .Where(profile => !string.IsNullOrWhiteSpace(profile.Locale)
                              && !available.Contains(profile.Locale))
            .Select(profile =>
                $"Profile '{profile.Id}' requires inactive locale '{profile.Locale}'. Enable its language pack and restart the application.")
            .ToArray();
    }
}
