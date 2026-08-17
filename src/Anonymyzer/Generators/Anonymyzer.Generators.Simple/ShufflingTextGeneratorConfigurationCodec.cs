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
    }
}
