namespace Anonymyzer.Console.Commands;

internal class GenerateAnonymyzerConfigurationCommandParameters : DbParameters
{
    /// <summary>
    /// Gets or sets path, where generated anonymyzer configuration file will be saved.
    /// </summary>
    public string ConfigurationFilePath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets, whether existing file could be overwritten or not
    /// </summary>
    public bool DoOverride { get; set; } = false;
}