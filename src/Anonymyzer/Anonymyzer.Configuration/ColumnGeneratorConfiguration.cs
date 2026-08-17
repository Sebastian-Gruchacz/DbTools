namespace Anonymyzer.Configuration;

using Newtonsoft.Json.Linq;

/// <summary>
/// Selects a generator profile and optional overrides for one column.
/// </summary>
public sealed class ColumnGeneratorConfiguration
{
    public string GeneratorType { get; set; } = string.Empty;

    public string GeneratorVersion { get; set; } = string.Empty;

    public string ProfileId { get; set; } = string.Empty;

    public JObject Options { get; set; } = new();
}
