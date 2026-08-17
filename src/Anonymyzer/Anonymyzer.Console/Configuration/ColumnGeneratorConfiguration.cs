namespace Anonymyzer.Console.Configuration;

using Newtonsoft.Json.Linq;

internal class ColumnGeneratorConfiguration
{
    public string Name { get; set; } = string.Empty;

    public JObject Options { get; set; }
}