namespace Anonymyzer.Generators.Simple;

public sealed class ShufflingTextGeneratorConfiguration
{
    public int Seed { get; set; }

    public int MinimumPopulation { get; set; } = 2;

    public bool PreserveNulls { get; set; } = true;
}
