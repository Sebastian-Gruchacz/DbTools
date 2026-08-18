namespace Anonymyzer.Generators.Simple;

using Anonymyzer.Base.Generation;

public sealed class TaxIdentifierGeneratorConfigurationCodec
    : GeneratorConfigurationCodec<TaxIdentifierGeneratorConfiguration>
{
    protected override TaxIdentifierGeneratorConfiguration CreateDefaultConfiguration() => new();

    protected override IEnumerable<string> ValidateConfiguration(TaxIdentifierGeneratorConfiguration configuration)
    {
        if (string.IsNullOrWhiteSpace(configuration.Locale))
        {
            yield return "Locale is required.";
        }

        if (string.IsNullOrWhiteSpace(configuration.Variant))
        {
            yield return "Variant is required.";
        }

        if (!Enum.IsDefined(configuration.Format))
        {
            yield return $"Unsupported tax-identifier format '{configuration.Format}'.";
        }
    }
}
