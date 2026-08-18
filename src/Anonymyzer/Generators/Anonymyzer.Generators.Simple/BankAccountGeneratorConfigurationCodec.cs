namespace Anonymyzer.Generators.Simple;

using Anonymyzer.Base.Generation;

public sealed class BankAccountGeneratorConfigurationCodec
    : GeneratorConfigurationCodec<BankAccountGeneratorConfiguration>
{
    protected override BankAccountGeneratorConfiguration CreateDefaultConfiguration() => new();

    protected override IEnumerable<string> ValidateConfiguration(BankAccountGeneratorConfiguration configuration)
    {
        if (string.IsNullOrWhiteSpace(configuration.Locale))
        {
            yield return "Locale is required.";
        }

        if (!Enum.IsDefined(configuration.Format))
        {
            yield return $"Unsupported bank-account format '{configuration.Format}'.";
        }
    }
}
