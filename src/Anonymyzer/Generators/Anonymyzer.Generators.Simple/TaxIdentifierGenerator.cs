namespace Anonymyzer.Generators.Simple;

using Anonymyzer.Base;
using Anonymyzer.Base.Generation;

public sealed class TaxIdentifierGenerator : GeneratorBase<TaxIdentifierGeneratorConfiguration>
{
    public const string GeneratorType = "TaxIdentifier";
    public const string GeneratorVersion = "1.0.0";
    public const string ValueOutput = "Value";

    private static readonly GeneratorDescriptor GeneratorDescriptor = new(
        GeneratorType,
        GeneratorVersion,
        "Tax identifier",
        GeneratorExecutionScope.Row,
        DbDataType.Text)
    {
        Outputs = [new GeneratorOutputDescriptor(ValueOutput, "Tax identifier", "Company.TaxId", Required: true)]
    };

    private static readonly TaxIdentifierGeneratorConfigurationCodec ConfigurationCodec = new();
    private readonly IReadOnlyDictionary<string, ITaxIdentifierLocaleDataProvider> _localeProviders;

    public TaxIdentifierGenerator(IEnumerable<ITaxIdentifierLocaleDataProvider> localeProviders)
    {
        ArgumentNullException.ThrowIfNull(localeProviders);
        _localeProviders = localeProviders.ToDictionary(provider => provider.Locale, StringComparer.OrdinalIgnoreCase);
    }

    public override GeneratorDescriptor Descriptor => GeneratorDescriptor;

    public override IGeneratorConfigurationCodec Configuration => ConfigurationCodec;

    protected override IReadOnlyList<GeneratorDataRequirement> GetDataRequirements(
        GeneratorBinding binding,
        TaxIdentifierGeneratorConfiguration configuration)
    {
        binding.GetRequiredOutput(ValueOutput);
        return Array.Empty<GeneratorDataRequirement>();
    }

    protected override ValueTask<IGeneratorSession> PrepareAsync(
        GeneratorPreparationContext context,
        TaxIdentifierGeneratorConfiguration configuration,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<string> errors = Configuration.Validate(configuration);
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, errors));
        }

        if (!_localeProviders.TryGetValue(configuration.Locale, out ITaxIdentifierLocaleDataProvider? localeProvider))
        {
            throw new InvalidOperationException($"Tax-identifier locale '{configuration.Locale}' is not installed.");
        }

        IReadOnlyList<string> providerErrors = localeProvider.Validate(configuration.Variant, configuration.Format);
        if (providerErrors.Count > 0)
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, providerErrors));
        }

        string columnName = context.Binding.GetRequiredOutput(ValueOutput);
        return ValueTask.FromResult<IGeneratorSession>(new Session(columnName, localeProvider, configuration));
    }

    private sealed class Session(
        string columnName,
        ITaxIdentifierLocaleDataProvider localeProvider,
        TaxIdentifierGeneratorConfiguration configuration) : IGeneratorSession
    {
        private long _nextOrdinal;

        public ValueTask ApplyAsync(IGeneratorRow row, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!configuration.PreserveNulls || row.GetValue(columnName) is not null)
            {
                long capacity = localeProvider.GetCapacity(configuration.Variant);
                if (_nextOrdinal >= capacity)
                {
                    throw new InvalidOperationException(
                        $"Tax-identifier locale '{localeProvider.Locale}' exhausted its " +
                        $"{capacity:N0} distinct synthetic values.");
                }

                row.SetValue(
                    columnName,
                    localeProvider.Generate(
                        _nextOrdinal,
                        configuration.Seed,
                        configuration.Variant,
                        configuration.Format));
                _nextOrdinal++;
            }

            return ValueTask.CompletedTask;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
