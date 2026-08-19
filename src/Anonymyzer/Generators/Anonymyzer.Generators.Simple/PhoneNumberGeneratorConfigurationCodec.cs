namespace Anonymyzer.Generators.Simple;

using Anonymyzer.Base.Generation;

public sealed class PhoneNumberGeneratorConfigurationCodec
    : GeneratorConfigurationCodec<PhoneNumberGeneratorConfiguration>
{
    protected override PhoneNumberGeneratorConfiguration CreateDefaultConfiguration() => new();

    protected override IEnumerable<string> ValidateConfiguration(PhoneNumberGeneratorConfiguration configuration)
    {
        if (string.IsNullOrWhiteSpace(configuration.Locale))
        {
            yield return "Locale is required.";
        }

        if (!Enum.IsDefined(configuration.Format))
        {
            yield return $"Unsupported phone-number format '{configuration.Format}'.";
        }
    }
}
