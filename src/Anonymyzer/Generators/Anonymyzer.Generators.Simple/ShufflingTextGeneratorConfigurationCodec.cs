namespace Anonymyzer.Generators.Simple;

using Anonymyzer.Base.Generation;

public sealed class ShufflingTextGeneratorConfigurationCodec
    : GeneratorConfigurationCodec<ShufflingTextGeneratorConfiguration>
{
    protected override ShufflingTextGeneratorConfiguration CreateDefaultConfiguration()
    {
        return new ShufflingTextGeneratorConfiguration();
    }

    protected override IEnumerable<string> ValidateConfiguration(ShufflingTextGeneratorConfiguration configuration)
    {
        if (configuration.MinimumPopulation < 2)
        {
            yield return "MinimumPopulation must be at least 2.";
        }

        if (configuration.MaximumInMemoryBytes < 1024 * 1024)
        {
            yield return "MaximumInMemoryBytes must be at least 1048576 (1 MiB).";
        }

        if (!Enum.IsDefined(configuration.OverflowStrategy))
        {
            yield return "OverflowStrategy is invalid.";
        }
    }
}
