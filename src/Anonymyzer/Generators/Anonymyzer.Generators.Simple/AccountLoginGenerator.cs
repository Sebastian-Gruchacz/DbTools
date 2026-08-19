namespace Anonymyzer.Generators.Simple;

using System.Globalization;
using Anonymyzer.Base;
using Anonymyzer.Base.Generation;

public sealed class AccountLoginGenerator : GeneratorBase<AccountLoginGeneratorConfiguration>
{
    public const string GeneratorType = "AccountLogin";
    public const string GeneratorVersion = "1.0.0";
    public const string ValueOutput = "Value";

    private static readonly GeneratorDescriptor GeneratorDescriptor = new(
        GeneratorType, GeneratorVersion, "Account login", GeneratorExecutionScope.Row, DbDataType.Text)
    {
        SupportsDeterministicReplay = true,
        Outputs = [new GeneratorOutputDescriptor(ValueOutput, "Login", "Account.Login", Required: true)]
    };
    private static readonly AccountLoginGeneratorConfigurationCodec ConfigurationCodec = new();

    public override GeneratorDescriptor Descriptor => GeneratorDescriptor;
    public override IGeneratorConfigurationCodec Configuration => ConfigurationCodec;

    protected override IReadOnlyList<GeneratorDataRequirement> GetDataRequirements(
        GeneratorBinding binding, AccountLoginGeneratorConfiguration configuration)
    {
        binding.GetRequiredOutput(ValueOutput);
        return configuration.Pattern == AccountLoginPattern.NameBased
            ? [new GeneratorDataRequirement("name-components", binding.Table,
                [configuration.FirstNameColumn, configuration.LastNameColumn],
                configuration.NameValueSource, RequiresCompleteScan: false)]
            : Array.Empty<GeneratorDataRequirement>();
    }

    protected override ValueTask<IGeneratorSession> PrepareAsync(
        GeneratorPreparationContext context, AccountLoginGeneratorConfiguration configuration,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<string> errors = Configuration.Validate(configuration);
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, errors));
        }

        return ValueTask.FromResult<IGeneratorSession>(
            new Session(context.Binding.GetRequiredOutput(ValueOutput), configuration));
    }

    private sealed class Session(string columnName, AccountLoginGeneratorConfiguration configuration) : IGeneratorSession
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
                throw new InvalidOperationException("AccountLogin exhausted the Int64 sequence range.");
            }

            string number = _next.ToString($"D{configuration.MinimumDigits}", CultureInfo.InvariantCulture);
            string[] parts = configuration.Pattern switch
            {
                AccountLoginPattern.Opaque => [Normalize(configuration.OpaquePrefix, "opaque prefix"), number],
                AccountLoginPattern.NameBased =>
                [
                    Normalize(row.GetValue(configuration.FirstNameColumn)?.ToString(), "first name"),
                    Normalize(row.GetValue(configuration.LastNameColumn)?.ToString(), "last name"),
                    number
                ],
                _ => throw new InvalidOperationException($"Unsupported login pattern '{configuration.Pattern}'.")
            };
            row.SetValue(columnName, string.Join(configuration.Separator, parts));
            _exhausted = _next == long.MaxValue;
            if (!_exhausted)
            {
                _next++;
            }

            return ValueTask.CompletedTask;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        private static string Normalize(string? value, string field)
        {
            string normalized = value is null ? string.Empty : EmailAddressGenerator.NormalizeToken(value);
            return normalized.Length > 0
                ? normalized
                : throw new InvalidOperationException($"The {field} has no ASCII letters or digits after normalization.");
        }
    }
}
