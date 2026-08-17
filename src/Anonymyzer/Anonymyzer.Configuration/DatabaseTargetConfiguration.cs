namespace Anonymyzer.Configuration;

/// <summary>
/// Non-secret identity of the detached database targeted by the configuration.
/// </summary>
public sealed class DatabaseTargetConfiguration
{
    public string DatabaseEngine { get; set; } = string.Empty;

    public string DatabaseName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the non-secret identifier stored in the detached-copy marker table.
    /// </summary>
    public string DetachedCopyMarkerId { get; set; } = string.Empty;
}
