namespace Anonymyzer.Generators.Simple;

public sealed class JsonPathRedactorGeneratorConfiguration
{
    public List<JsonPathRedactionRuleConfiguration> Rules { get; set; } = [];

    public bool RequireEveryPath { get; set; }
}

public sealed class JsonPathRedactionRuleConfiguration
{
    public string Path { get; set; } = string.Empty;

    public string ReplacementJson { get; set; } = "null";
}
