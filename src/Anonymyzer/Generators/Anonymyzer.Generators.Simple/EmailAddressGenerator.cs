namespace Anonymyzer.Generators.Simple;

using System.Globalization;
using System.Text;
using Anonymyzer.Base;
using Anonymyzer.Base.Generation;

public sealed class EmailAddressGenerator : GeneratorBase<EmailAddressGeneratorConfiguration>
{
    public const string GeneratorType = "EmailAddress";
    public const string GeneratorVersion = "1.0.0";
    public const string ValueOutput = "Value";

    private const int MaximumDomainLength = 230;
    private const int MaximumEmailLength = 254;
    private const int MaximumLocalPartLength = 64;

    private static readonly GeneratorDescriptor GeneratorDescriptor = new(
        GeneratorType,
        GeneratorVersion,
        "E-mail address",
        GeneratorExecutionScope.Row,
        DbDataType.Text)
    {
        Outputs = [new GeneratorOutputDescriptor(ValueOutput, "E-mail", "Contact.Email", Required: true)]
    };

    private static readonly EmailAddressGeneratorConfigurationCodec ConfigurationCodec = new();

    public override GeneratorDescriptor Descriptor => GeneratorDescriptor;

    public override IGeneratorConfigurationCodec Configuration => ConfigurationCodec;

    public static bool CanNormalizeToken(string? value) =>
        value is not null && NormalizeToken(value).Length > 0;

    public static bool IsValidDomain(string? domain)
    {
        if (string.IsNullOrWhiteSpace(domain)
            || domain.Length > MaximumDomainLength
            || domain.Any(character => character > 127))
        {
            return false;
        }

        string[] labels = domain.Split('.');
        return labels.Length >= 2
            && labels.All(label => label.Length is >= 1 and <= 63
                && label[0] != '-'
                && label[^1] != '-'
                && label.All(character => char.IsAsciiLetterOrDigit(character) || character == '-'));
    }

    protected override IReadOnlyList<GeneratorDataRequirement> GetDataRequirements(
        GeneratorBinding binding,
        EmailAddressGeneratorConfiguration configuration)
    {
        binding.GetRequiredOutput(ValueOutput);
        if (configuration.Pattern != EmailAddressPattern.NameBased)
        {
            return Array.Empty<GeneratorDataRequirement>();
        }

        return
        [
            new GeneratorDataRequirement(
                "name-components",
                binding.Table,
                [configuration.FirstNameColumn, configuration.LastNameColumn],
                configuration.NameValueSource,
                RequiresCompleteScan: false)
        ];
    }

    protected override ValueTask<IGeneratorSession> PrepareAsync(
        GeneratorPreparationContext context,
        EmailAddressGeneratorConfiguration configuration,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<string> errors = Configuration.Validate(configuration);
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, errors));
        }

        string columnName = context.Binding.GetRequiredOutput(ValueOutput);
        return ValueTask.FromResult<IGeneratorSession>(new Session(columnName, configuration));
    }

    internal static string NormalizeToken(string value)
    {
        var result = new StringBuilder(value.Length);
        foreach (char character in value.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD))
        {
            UnicodeCategory category = CharUnicodeInfo.GetUnicodeCategory(character);
            if (category == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            char normalized = character switch
            {
                'ł' => 'l',
                'đ' => 'd',
                'ð' => 'd',
                'þ' => 't',
                'æ' => 'a',
                'ø' => 'o',
                _ => character
            };
            if (char.IsAsciiLetterOrDigit(normalized))
            {
                result.Append(normalized);
            }
        }

        return result.ToString();
    }

    private sealed class Session(
        string columnName,
        EmailAddressGeneratorConfiguration configuration) : IGeneratorSession
    {
        private long _next = configuration.StartAt;
        private bool _exhausted;

        public ValueTask ApplyAsync(IGeneratorRow row, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (configuration.PreserveNulls && row.GetValue(columnName) is null)
            {
                return ValueTask.CompletedTask;
            }

            if (_exhausted)
            {
                throw new InvalidOperationException("EmailAddress exhausted the Int64 sequence range.");
            }

            string number = _next.ToString($"D{configuration.MinimumDigits}", CultureInfo.InvariantCulture);
            string localPart = configuration.Pattern switch
            {
                EmailAddressPattern.Opaque => BuildOpaqueLocalPart(number),
                EmailAddressPattern.NameBased => BuildNameBasedLocalPart(row, number),
                _ => throw new InvalidOperationException($"Unsupported e-mail pattern '{configuration.Pattern}'.")
            };
            row.SetValue(columnName, $"{localPart}@{configuration.Domain.ToLowerInvariant()}");

            if (_next == long.MaxValue)
            {
                _exhausted = true;
            }
            else
            {
                _next++;
            }

            return ValueTask.CompletedTask;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        private string BuildOpaqueLocalPart(string number)
        {
            string prefix = NormalizeRequired(configuration.OpaquePrefix, "opaque prefix");
            return FitLocalPart([prefix, number]);
        }

        private string BuildNameBasedLocalPart(IGeneratorRow row, string number)
        {
            string firstName = NormalizeRequired(
                row.GetValue(configuration.FirstNameColumn)?.ToString(),
                $"first name column '{configuration.FirstNameColumn}'");
            string lastName = NormalizeRequired(
                row.GetValue(configuration.LastNameColumn)?.ToString(),
                $"last name column '{configuration.LastNameColumn}'");
            return FitLocalPart([firstName, lastName, number]);
        }

        private string FitLocalPart(IReadOnlyList<string> parts)
        {
            int maximumLength = Math.Min(
                MaximumLocalPartLength,
                MaximumEmailLength - configuration.Domain.Length - 1);
            int separators = parts.Count - 1;
            int availableCharacters = maximumLength - separators;
            if (availableCharacters < parts.Count)
            {
                throw new InvalidOperationException("Configured domain leaves too little space for a valid local part.");
            }

            var fitted = parts.ToArray();
            int overflow = fitted.Sum(part => part.Length) - availableCharacters;
            for (int index = 0; overflow > 0 && index < fitted.Length - 1; index++)
            {
                int removable = Math.Min(overflow, fitted[index].Length - 1);
                fitted[index] = fitted[index][..(fitted[index].Length - removable)];
                overflow -= removable;
            }

            if (overflow > 0)
            {
                throw new InvalidOperationException("Sequence number is too long for the configured e-mail domain.");
            }

            return string.Join('.', fitted);
        }

        private static string NormalizeRequired(string? value, string field)
        {
            string normalized = value is null ? string.Empty : NormalizeToken(value);
            return normalized.Length > 0
                ? normalized
                : throw new InvalidOperationException($"The {field} has no characters usable in an e-mail address.");
        }
    }
}
