namespace Anonymyzer.Generators.Simple;

using Anonymyzer.Base.Generation;

public sealed class ReferencePseudonymGeneratorConfigurationCodec
    : GeneratorConfigurationCodec<ReferencePseudonymGeneratorConfiguration>
{
    protected override ReferencePseudonymGeneratorConfiguration CreateDefaultConfiguration() => new();

    protected override IEnumerable<string> ValidateConfiguration(
        ReferencePseudonymGeneratorConfiguration configuration)
    {
        if (string.IsNullOrWhiteSpace(configuration.ReferenceColumn))
        {
            yield return "ReferenceColumn is required.";
        }

        if (string.IsNullOrWhiteSpace(configuration.LookupSchema))
        {
            yield return "LookupSchema is required.";
        }

        if (string.IsNullOrWhiteSpace(configuration.LookupTable))
        {
            yield return "LookupTable is required.";
        }

        if (string.IsNullOrWhiteSpace(configuration.LookupKeyColumn))
        {
            yield return "LookupKeyColumn is required.";
        }

        if (configuration.Prefix is null)
        {
            yield return "Prefix cannot be null.";
        }

        if (string.IsNullOrWhiteSpace(configuration.KeyEnvironmentVariable))
        {
            yield return "KeyEnvironmentVariable is required.";
        }

        if (configuration.HashLength is < 16 or > 64)
        {
            yield return "HashLength must be between 16 and 64 hexadecimal characters.";
        }

        if (configuration.MaximumInMemoryBytes < 1024 * 1024)
        {
            yield return "MaximumInMemoryBytes must be at least 1 MiB.";
        }
    }
}
