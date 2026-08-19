namespace Anonymyzer.Generators.Simple;

using Anonymyzer.Base;
using Anonymyzer.Base.Generation;

public sealed class PhoneNumberGenerator : GeneratorBase<PhoneNumberGeneratorConfiguration>
{
    public const string GeneratorType = "PhoneNumber";
    public const string GeneratorVersion = "1.0.0";
    public const string ValueOutput = "Value";

    private static readonly GeneratorDescriptor GeneratorDescriptor = new(
        GeneratorType,
        GeneratorVersion,
        "Phone number",
        GeneratorExecutionScope.Row,
        DbDataType.Text)
    {
        SupportsDeterministicReplay = true,
        Outputs = [new GeneratorOutputDescriptor(ValueOutput, "Phone number", "Contact.Phone", Required: true)]
    };

    private static readonly PhoneNumberGeneratorConfigurationCodec ConfigurationCodec = new();
    private readonly IReadOnlyDictionary<string, IPhoneNumberLocaleDataProvider> _localeProviders;

    public PhoneNumberGenerator(IEnumerable<IPhoneNumberLocaleDataProvider> localeProviders)
    {
        ArgumentNullException.ThrowIfNull(localeProviders);
        _localeProviders = localeProviders.ToDictionary(provider => provider.Locale, StringComparer.OrdinalIgnoreCase);
    }

    public override GeneratorDescriptor Descriptor => GeneratorDescriptor;

    public override IGeneratorConfigurationCodec Configuration => ConfigurationCodec;

    protected override IReadOnlyList<GeneratorDataRequirement> GetDataRequirements(
        GeneratorBinding binding,
        PhoneNumberGeneratorConfiguration configuration)
    {
        binding.GetRequiredOutput(ValueOutput);
        return Array.Empty<GeneratorDataRequirement>();
    }

    protected override ValueTask<IGeneratorSession> PrepareAsync(
        GeneratorPreparationContext context,
        PhoneNumberGeneratorConfiguration configuration,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<string> errors = Configuration.Validate(configuration);
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, errors));
        }

        if (!_localeProviders.TryGetValue(configuration.Locale, out IPhoneNumberLocaleDataProvider? localeProvider))
        {
            throw new InvalidOperationException($"Phone-number locale '{configuration.Locale}' is not installed.");
        }

        string columnName = context.Binding.GetRequiredOutput(ValueOutput);
        return ValueTask.FromResult<IGeneratorSession>(new Session(columnName, localeProvider, configuration));
    }

    private sealed class Session(
        string columnName,
        IPhoneNumberLocaleDataProvider localeProvider,
        PhoneNumberGeneratorConfiguration configuration) : IGeneratorSession
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
                        $"Phone-number locale '{localeProvider.Locale}' exhausted its " +
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
