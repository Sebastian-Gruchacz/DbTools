namespace Anonymyzer.Generators.Simple;

using Anonymyzer.Base.Generation;

public sealed class UuidGeneratorConfigurationCodec : GeneratorConfigurationCodec<UuidGeneratorConfiguration>
{
    protected override UuidGeneratorConfiguration CreateDefaultConfiguration() => new();

    protected override IEnumerable<string> ValidateConfiguration(UuidGeneratorConfiguration configuration)
    {
        if (string.IsNullOrWhiteSpace(configuration.Seed))
        {
            yield return "Seed is required.";
        }
        else if (configuration.Seed.Length > 1024)
        {
            yield return "Seed cannot exceed 1024 characters.";
        }

        if (configuration.StartAt < 0)
        {
            yield return "StartAt cannot be negative.";
        }

        if (!Enum.IsDefined(configuration.Format))
        {
            yield return $"Unsupported UUID format '{configuration.Format}'.";
        }
    }
}
