namespace Anonymyzer.Generators.Simple;

using Anonymyzer.Base.Generation;

public sealed class SequentialTextGeneratorConfigurationCodec
    : GeneratorConfigurationCodec<SequentialTextGeneratorConfiguration>
{
    protected override SequentialTextGeneratorConfiguration CreateDefaultConfiguration() => new();

    protected override IEnumerable<string> ValidateConfiguration(SequentialTextGeneratorConfiguration configuration)
    {
        if (configuration.Prefix is null)
        {
            yield return "Prefix cannot be null.";
        }

        if (configuration.Suffix is null)
        {
            yield return "Suffix cannot be null.";
        }

        if (configuration.StartAt < 0)
        {
            yield return "StartAt cannot be negative.";
        }

        if (configuration.MinimumDigits is < 1 or > 32)
        {
            yield return "MinimumDigits must be between 1 and 32.";
        }
    }
}
