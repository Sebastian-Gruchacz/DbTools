namespace Anonymyzer.Generators.Simple;

public sealed class SequentialTextGeneratorConfiguration
{
    public string Prefix { get; set; } = "anon-";

    public string Suffix { get; set; } = string.Empty;

    public long StartAt { get; set; } = 1;

    public int MinimumDigits { get; set; } = 8;

    public bool PreserveNulls { get; set; } = true;
}
