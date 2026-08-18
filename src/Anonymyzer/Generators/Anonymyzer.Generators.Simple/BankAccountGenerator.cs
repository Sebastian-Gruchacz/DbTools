namespace Anonymyzer.Generators.Simple;

using Anonymyzer.Base;
using Anonymyzer.Base.Generation;

public sealed class BankAccountGenerator : GeneratorBase<BankAccountGeneratorConfiguration>
{
    public const string GeneratorType = "BankAccount";
    public const string GeneratorVersion = "1.0.0";
    public const string ValueOutput = "Value";

    private static readonly GeneratorDescriptor GeneratorDescriptor = new(
        GeneratorType,
        GeneratorVersion,
        "Bank account / IBAN",
        GeneratorExecutionScope.Row,
        DbDataType.Text)
    {
        Outputs = [new GeneratorOutputDescriptor(
            ValueOutput,
            "Bank account / IBAN",
            "Financial.BankAccount",
            Required: true)]
    };

    private static readonly BankAccountGeneratorConfigurationCodec ConfigurationCodec = new();
    private readonly IReadOnlyDictionary<string, IBankAccountLocaleDataProvider> _localeProviders;

    public BankAccountGenerator(IEnumerable<IBankAccountLocaleDataProvider> localeProviders)
    {
        ArgumentNullException.ThrowIfNull(localeProviders);
        _localeProviders = localeProviders.ToDictionary(provider => provider.Locale, StringComparer.OrdinalIgnoreCase);
    }

    public override GeneratorDescriptor Descriptor => GeneratorDescriptor;

    public override IGeneratorConfigurationCodec Configuration => ConfigurationCodec;

    protected override IReadOnlyList<GeneratorDataRequirement> GetDataRequirements(
        GeneratorBinding binding,
        BankAccountGeneratorConfiguration configuration)
    {
        binding.GetRequiredOutput(ValueOutput);
        return Array.Empty<GeneratorDataRequirement>();
    }

    protected override ValueTask<IGeneratorSession> PrepareAsync(
        GeneratorPreparationContext context,
        BankAccountGeneratorConfiguration configuration,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<string> errors = Configuration.Validate(configuration);
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, errors));
        }

        if (!_localeProviders.TryGetValue(configuration.Locale, out IBankAccountLocaleDataProvider? localeProvider))
        {
            throw new InvalidOperationException($"Bank-account locale '{configuration.Locale}' is not installed.");
        }

        IReadOnlyList<string> providerErrors = localeProvider.Validate(configuration.Format);
        if (providerErrors.Count > 0)
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, providerErrors));
        }

        string columnName = context.Binding.GetRequiredOutput(ValueOutput);
        return ValueTask.FromResult<IGeneratorSession>(new Session(columnName, localeProvider, configuration));
    }

    private sealed class Session(
        string columnName,
        IBankAccountLocaleDataProvider localeProvider,
        BankAccountGeneratorConfiguration configuration) : IGeneratorSession
    {
        private long _nextOrdinal;

        public ValueTask ApplyAsync(IGeneratorRow row, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!configuration.PreserveNulls || row.GetValue(columnName) is not null)
            {
                if (_nextOrdinal >= localeProvider.Capacity)
                {
                    throw new InvalidOperationException(
                        $"Bank-account locale '{localeProvider.Locale}' exhausted its " +
                        $"{localeProvider.Capacity:N0} distinct synthetic values.");
                }

                row.SetValue(
                    columnName,
                    localeProvider.Generate(_nextOrdinal, configuration.Seed, configuration.Format));
                _nextOrdinal++;
            }

            return ValueTask.CompletedTask;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
