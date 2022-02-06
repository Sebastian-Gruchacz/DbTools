namespace Anonymyzer.Console.Configuration;

using Anonymyzer.Base;
using Anonymyzer.Base.Generation;
using Anonymyzer.Console.GenerateConfiguration;

/// <summary>
/// Configuration for anonymyzation operation
/// </summary>
internal class AnonymyzationConfiguration
{
    public string Version { get; set; } = @"0.1.0";
    public TableProcessingOptions[] Tables { get; set; } = Array.Empty<TableProcessingOptions>();
    public Dictionary<string, GeneratorConfiguration> Generators { get; set; } = new();
}