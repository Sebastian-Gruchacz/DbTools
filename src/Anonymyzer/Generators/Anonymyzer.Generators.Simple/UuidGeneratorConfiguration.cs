namespace Anonymyzer.Generators.Simple;

public sealed class UuidGeneratorConfiguration
{
    public string Seed { get; set; } = "anonymyzer";

    public long StartAt { get; set; }

    public UuidTextFormat Format { get; set; } = UuidTextFormat.Hyphenated;

    public bool Uppercase { get; set; }

    public bool PreserveNulls { get; set; } = true;
}
