namespace Anonymyzer.Console.Configuration;

using System.Text.Json.Nodes;
using Newtonsoft.Json.Linq;

internal class ColumnGeneratorConfiguration
{
    public string Name { get; set; } = string.Empty;

    public JObject Options { get; set; }
}