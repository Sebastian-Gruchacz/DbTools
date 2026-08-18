namespace Anonymyzer.Generators.Simple;

using Anonymyzer.Base.Generation;

public sealed class CompanyNameGeneratorConfigurationCodec
    : GeneratorConfigurationCodec<CompanyNameGeneratorConfiguration>
{
    protected override CompanyNameGeneratorConfiguration CreateDefaultConfiguration() => new();

    protected override IEnumerable<string> ValidateConfiguration(CompanyNameGeneratorConfiguration configuration)
    {
        if (string.IsNullOrWhiteSpace(configuration.Locale))
        {
            yield return "Locale is required.";
        }

        if (string.IsNullOrWhiteSpace(configuration.SyntheticMarker))
        {
            yield return "SyntheticMarker is required so generated companies remain visibly fictional.";
        }
        else if (configuration.SyntheticMarker.Length > 32
                 || configuration.SyntheticMarker.Any(char.IsControl))
        {
            yield return "SyntheticMarker must contain at most 32 characters and no control characters.";
        }
    }
}
