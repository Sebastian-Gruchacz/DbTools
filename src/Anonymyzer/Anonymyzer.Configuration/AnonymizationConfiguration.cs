namespace Anonymyzer.Configuration;

/// <summary>
/// Describes an anonymization plan. It never contains database credentials.
/// </summary>
public sealed class AnonymizationConfiguration
{
    public const string CurrentVersion = "0.3.0";

    public string Version { get; set; } = CurrentVersion;

    public DatabaseTargetConfiguration Database { get; set; } = new();

    public List<GeneratorProfileConfiguration> GeneratorProfiles { get; set; } = new();

    public List<TableProcessingOptions> Tables { get; set; } = new();
}
