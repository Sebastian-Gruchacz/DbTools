namespace Anonymyzer.Console.Configuration;

using Anonymyzer.Console.GenerateConfiguration;
using Newtonsoft.Json.Linq;

/// <summary>
/// Configuration for anonymyzation operation
/// </summary>
internal class AnonymyzationConfiguration
{
    public string Version { get; set; } = @"0.2.0";

    public DatabaseTargetConfiguration DbConfiguration { get; set; } = new();

    public Dictionary<string, JObject> Generators { get; set; } = new();

    public TableProcessingOptions[] Tables { get; set; } = Array.Empty<TableProcessingOptions>();
}
