namespace Anonymyzer.Generators.Simple;

public sealed class FixedTextGeneratorConfiguration
{
    public string Value { get; set; } = "REDACTED";

    public bool PreserveNulls { get; set; } = true;
}
