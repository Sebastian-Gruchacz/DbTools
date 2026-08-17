namespace Anonymyzer.Configuration;

using Newtonsoft.Json.Linq;

/// <summary>
/// A named, reusable parameter set for a generator type.
/// </summary>
public sealed class GeneratorProfileConfiguration
{
    public string Id { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string GeneratorType { get; set; } = string.Empty;

    public string GeneratorVersion { get; set; } = string.Empty;

    public string Locale { get; set; } = string.Empty;

    public JObject Options { get; set; } = new();
}
