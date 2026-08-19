namespace Anonymyzer.Configuration;

using Newtonsoft.Json.Linq;

public sealed class GeneratorProfileMerger
{
    public GeneratorProfileMergeResult Merge(
        IList<GeneratorProfileConfiguration> target,
        IEnumerable<GeneratorProfileConfiguration> builtInProfiles)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(builtInProfiles);

        int markedFileProfiles = 0;
        foreach (GeneratorProfileConfiguration profile in target.Where(profile =>
                     string.IsNullOrWhiteSpace(profile.Origin)))
        {
            profile.Origin = "Configuration file";
            markedFileProfiles++;
        }

        int added = 0;
        int collisions = 0;
        int updated = 0;
        foreach (GeneratorProfileConfiguration template in builtInProfiles)
        {
            GeneratorProfileConfiguration? existingBuiltIn = target.FirstOrDefault(profile =>
                IsManagedOrigin(profile.Origin)
                && profile.GeneratorType.Equals(template.GeneratorType, StringComparison.OrdinalIgnoreCase)
                && profile.GeneratorVersion.Equals(template.GeneratorVersion, StringComparison.Ordinal)
                && EffectiveLocale(profile).Equals(template.Locale, StringComparison.OrdinalIgnoreCase));
            if (existingBuiltIn is not null)
            {
                bool changed = !existingBuiltIn.DisplayName.Equals(template.DisplayName, StringComparison.Ordinal)
                               || !existingBuiltIn.Locale.Equals(template.Locale, StringComparison.Ordinal)
                               || !existingBuiltIn.Origin.Equals(template.Origin, StringComparison.Ordinal)
                               || !JToken.DeepEquals(existingBuiltIn.Options, template.Options);
                existingBuiltIn.DisplayName = template.DisplayName;
                existingBuiltIn.Locale = template.Locale;
                existingBuiltIn.Origin = template.Origin;
                existingBuiltIn.Options = (JObject)template.Options.DeepClone();
                if (changed)
                {
                    updated++;
                }

                continue;
            }

            string id = template.Id;
            if (target.Any(profile => profile.Id.Equals(id, StringComparison.OrdinalIgnoreCase)))
            {
                collisions++;
                id = CreateUniqueId(target, $"{id}:BuiltIn");
            }

            target.Add(Clone(template, id));
            added++;
        }

        return new GeneratorProfileMergeResult(added, updated, collisions, markedFileProfiles);
    }

    private static string CreateUniqueId(IEnumerable<GeneratorProfileConfiguration> profiles, string baseId)
    {
        string candidate = baseId;
        int suffix = 2;
        while (profiles.Any(profile => profile.Id.Equals(candidate, StringComparison.OrdinalIgnoreCase)))
        {
            candidate = $"{baseId}:{suffix++}";
        }

        return candidate;
    }

    private static bool IsManagedOrigin(string origin) =>
        origin.Equals("Built-in", StringComparison.OrdinalIgnoreCase)
        || origin.StartsWith("Language pack:", StringComparison.OrdinalIgnoreCase);

    private static string EffectiveLocale(GeneratorProfileConfiguration profile) =>
        string.IsNullOrWhiteSpace(profile.Locale)
            ? profile.Options.Value<string>("Locale") ?? string.Empty
            : profile.Locale;

    private static GeneratorProfileConfiguration Clone(GeneratorProfileConfiguration source, string id) => new()
    {
        Id = id,
        DisplayName = source.DisplayName,
        GeneratorType = source.GeneratorType,
        GeneratorVersion = source.GeneratorVersion,
        Locale = source.Locale,
        Origin = source.Origin,
        Options = (JObject)source.Options.DeepClone()
    };
}

public sealed record GeneratorProfileMergeResult(
    int AddedProfiles,
    int UpdatedProfiles,
    int IdCollisions,
    int MarkedFileProfiles)
{
    public bool Changed => AddedProfiles > 0 || UpdatedProfiles > 0 || MarkedFileProfiles > 0;
}
