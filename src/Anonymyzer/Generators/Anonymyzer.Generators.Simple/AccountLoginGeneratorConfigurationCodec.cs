namespace Anonymyzer.Generators.Simple;

using Anonymyzer.Base.Generation;

public sealed class AccountLoginGeneratorConfigurationCodec
    : GeneratorConfigurationCodec<AccountLoginGeneratorConfiguration>
{
    protected override AccountLoginGeneratorConfiguration CreateDefaultConfiguration() => new();

    protected override IEnumerable<string> ValidateConfiguration(AccountLoginGeneratorConfiguration configuration)
    {
        if (!Enum.IsDefined(configuration.Pattern))
        {
            yield return $"Unsupported login pattern '{configuration.Pattern}'.";
        }

        if (configuration.Separator.Length > 3
            || configuration.Separator.Any(character => character is not ('.' or '_' or '-')))
        {
            yield return "Separator may contain at most three '.', '_' or '-' characters.";
        }

        if (configuration.StartAt < 0)
        {
            yield return "StartAt cannot be negative.";
        }

        if (configuration.MinimumDigits is < 1 or > 32)
        {
            yield return "MinimumDigits must be between 1 and 32.";
        }

        if (configuration.Pattern == AccountLoginPattern.Opaque
            && !EmailAddressGenerator.CanNormalizeToken(configuration.OpaquePrefix))
        {
            yield return "OpaquePrefix must contain at least one ASCII letter or digit after normalization.";
        }

        if (configuration.Pattern == AccountLoginPattern.NameBased)
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
                && configuration.FirstNameColumn.Equals(configuration.LastNameColumn, StringComparison.OrdinalIgnoreCase))
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
