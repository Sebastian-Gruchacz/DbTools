namespace Anonymyzer.Console.Configuration;

/// <summary>
/// Non-secret identity of the detached database targeted by the configuration.
/// </summary>
internal sealed class DatabaseTargetConfiguration
{
    public string DatabaseEngine { get; set; } = string.Empty;

    public string DatabaseName { get; set; } = string.Empty;
}
