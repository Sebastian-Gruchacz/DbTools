namespace Anonymyzer.Console.GenerateConfiguration;

internal class GenerateAnonymyzerConfigurationCommandParameters
{
    /// <summary>
    /// Gets or sets name of used DatabaseEngine
    /// </summary>
    public string DatabaseEngine { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets connection string to the DB
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets path, where generated anonymyzer configuration file will be saved.
    /// </summary>
    public string ConfigurationFilePath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets, whether existing file could be overwritten or not
    /// </summary>
    public bool DoOverride { get; set; } = false;
}