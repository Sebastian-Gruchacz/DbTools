namespace Anonymyzer.Generators.Person;

using Anonymyzer.Base.Generation;

public sealed class GenderGeneratorConfigurationCodec
    : GeneratorConfigurationCodec<GenderGeneratorConfiguration>
{
    protected override GenderGeneratorConfiguration CreateDefaultConfiguration() => new();

    protected override IEnumerable<string> ValidateConfiguration(GenderGeneratorConfiguration configuration)
    {
        if (string.IsNullOrWhiteSpace(configuration.FemaleValue))
        {
            yield return "FemaleValue is required.";
        }

        if (string.IsNullOrWhiteSpace(configuration.MaleValue))
        {
            yield return "MaleValue is required.";
        }

        if (!string.IsNullOrWhiteSpace(configuration.FemaleValue)
            && configuration.FemaleValue.Equals(configuration.MaleValue, StringComparison.OrdinalIgnoreCase))
        {
            yield return "FemaleValue and MaleValue must be different.";
        }

        if (configuration.FemalePercentage is < 0 or > 100)
        {
            yield return "FemalePercentage must be between 0 and 100.";
        }
    }
}
