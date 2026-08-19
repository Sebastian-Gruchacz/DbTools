namespace Anonymyzer.Generators.Simple;

public sealed class ShufflingTextGeneratorConfiguration
{
    public const long DefaultMaximumInMemoryBytes = 64L * 1024 * 1024;

    public int Seed { get; set; }

    public int MinimumPopulation { get; set; } = 2;

    public bool PreserveNulls { get; set; } = true;

    public long MaximumInMemoryBytes { get; set; } = DefaultMaximumInMemoryBytes;

    public ShuffleOverflowStrategy OverflowStrategy { get; set; } = ShuffleOverflowStrategy.Fail;
}

public enum ShuffleOverflowStrategy
{
    Fail,
    EncryptedTemporaryFiles
}
