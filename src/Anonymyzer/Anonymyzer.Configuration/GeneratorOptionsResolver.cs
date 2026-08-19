namespace Anonymyzer.Configuration;

using Newtonsoft.Json.Linq;

public static class GeneratorOptionsResolver
{
    public static JObject ResolveGroupOptions(
        GeneratorProfileConfiguration profile,
        GenerationGroupConfiguration group)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(group);

        var options = (JObject)profile.Options.DeepClone();
        if (!string.IsNullOrWhiteSpace(group.Locale))
        {
            options[nameof(group.Locale)] = group.Locale.Trim();
        }

        return options;
    }
}
