namespace Anonymyzer.Generators.Simple;

using Anonymyzer.Base.Generation;

public sealed class EmailAddressGeneratorConfigurationCodec
    : GeneratorConfigurationCodec<EmailAddressGeneratorConfiguration>
{
    protected override EmailAddressGeneratorConfiguration CreateDefaultConfiguration() => new();

    protected override IEnumerable<string> ValidateConfiguration(EmailAddressGeneratorConfiguration configuration)
    {
        if (!Enum.IsDefined(configuration.Pattern))
        {
            yield return $"Unsupported e-mail pattern '{configuration.Pattern}'.";
        }

        if (!EmailAddressGenerator.IsValidDomain(configuration.Domain))
        {
            yield return "Domain must be an ASCII DNS name no longer than 230 characters.";
        }

        if (configuration.StartAt < 0)
        {
            yield return "StartAt cannot be negative.";
        }

        if (configuration.MinimumDigits is < 1 or > 32)
        {
            yield return "MinimumDigits must be between 1 and 32.";
        }

        int sequenceDigits = Math.Max(configuration.MinimumDigits, 19);
        int minimumLocalPartLength = sequenceDigits
            + (configuration.Pattern == EmailAddressPattern.NameBased ? 4 : 2);
        if (EmailAddressGenerator.IsValidDomain(configuration.Domain)
            && (minimumLocalPartLength > 64
                || minimumLocalPartLength + configuration.Domain.Length + 1 > 254))
        {
            yield return "Domain and MinimumDigits leave too little space for a valid e-mail local part.";
        }

        if (configuration.Pattern == EmailAddressPattern.Opaque
            && !EmailAddressGenerator.CanNormalizeToken(configuration.OpaquePrefix))
        {
            yield return "OpaquePrefix must contain at least one ASCII letter or digit after normalization.";
        }

        if (configuration.Pattern == EmailAddressPattern.NameBased)
        {
            if (string.IsNullOrWhiteSpace(configuration.FirstNameColumn))
            {
                yield return "FirstNameColumn is required for the NameBased pattern.";
            }

            if (string.IsNullOrWhiteSpace(configuration.LastNameColumn))
            {
                yield return "LastNameColumn is required for the NameBased pattern.";
            }

            if (!string.IsNullOrWhiteSpace(configuration.FirstNameColumn)
                && configuration.FirstNameColumn.Equals(
                    configuration.LastNameColumn,
                    StringComparison.OrdinalIgnoreCase))
            {
                yield return "FirstNameColumn and LastNameColumn must be different.";
            }

            if (!Enum.IsDefined(configuration.NameValueSource))
            {
                yield return $"Unsupported name value source '{configuration.NameValueSource}'.";
            }
        }
    }
}
