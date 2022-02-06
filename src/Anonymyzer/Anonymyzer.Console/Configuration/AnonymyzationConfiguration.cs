namespace Anonymyzer.Console.Configuration;

using Newtonsoft.Json.Linq;

/// <summary>
/// Configuration for anonymyzation operation
/// </summary>
internal class AnonymyzationConfiguration
{
    public string Version { get; set; } = @"0.1.0";
    public TableProcessingOptions[] Tables { get; set; } = Array.Empty<TableProcessingOptions>();
    public Dictionary<string, JObject> Generators { get; set; } = new();
}