namespace Anonymyzer.Configuration;

/// <summary>
/// Maps several outputs of one coherent generator invocation to table columns.
/// </summary>
public sealed class GenerationGroupConfiguration
{
    public string Id { get; set; } = string.Empty;

    public string GeneratorType { get; set; } = string.Empty;

    public string GeneratorVersion { get; set; } = string.Empty;

    public string ProfileId { get; set; } = string.Empty;

    public string Locale { get; set; } = string.Empty;

    public Dictionary<string, string> Bindings { get; set; } = new();
}
