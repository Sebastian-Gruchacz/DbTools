namespace Anonymyzer.Generators.Person;

using Anonymyzer.Base;
using Anonymyzer.Base.Generation;

public sealed class NationalIdentifierGenerator : GeneratorBase<NationalIdentifierGeneratorConfiguration>
{
    public const string GeneratorType = "NationalIdentifier";
    public const string GeneratorVersion = "1.0.0";
    public const string ValueOutput = "Value";

    private static readonly GeneratorDescriptor GeneratorDescriptor = new(
        GeneratorType,
        GeneratorVersion,
        "National identifier",
        GeneratorExecutionScope.Row,
        DbDataType.Text)
    {
        Outputs = [new GeneratorOutputDescriptor(ValueOutput, "National identifier", "Person.NationalId", Required: true)]
    };

    private static readonly NationalIdentifierGeneratorConfigurationCodec ConfigurationCodec = new();
    private readonly IReadOnlyDictionary<string, INationalIdentifierLocaleDataProvider> _localeProviders;

    public NationalIdentifierGenerator(IEnumerable<INationalIdentifierLocaleDataProvider> localeProviders)
    {
        _localeProviders = localeProviders.ToDictionary(provider => provider.Locale, StringComparer.OrdinalIgnoreCase);
    }

    public override GeneratorDescriptor Descriptor => GeneratorDescriptor;

    public override IGeneratorConfigurationCodec Configuration => ConfigurationCodec;

    protected override IReadOnlyList<GeneratorDataRequirement> GetDataRequirements(
        GeneratorBinding binding,
        NationalIdentifierGeneratorConfiguration configuration)
    {
        binding.GetRequiredOutput(ValueOutput);
        return Array.Empty<GeneratorDataRequirement>();
    }

    protected override ValueTask<IGeneratorSession> PrepareAsync(
        GeneratorPreparationContext context,
        NationalIdentifierGeneratorConfiguration configuration,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<string> errors = Configuration.Validate(configuration);
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, errors));
        }

        if (!_localeProviders.TryGetValue(configuration.Locale, out INationalIdentifierLocaleDataProvider? provider))
        {
            throw new InvalidOperationException($"National-identifier locale '{configuration.Locale}' is not installed.");
        }

        NationalIdentifierGeneratorConfigurationCodec.TryParseDate(configuration.MinimumBirthDate, out DateOnly minimum);
        NationalIdentifierGeneratorConfigurationCodec.TryParseDate(configuration.MaximumBirthDate, out DateOnly maximum);
        string columnName = context.Binding.GetRequiredOutput(ValueOutput);
        long capacity = provider.GetCapacity(minimum, maximum, configuration.Gender);
        return ValueTask.FromResult<IGeneratorSession>(
            new Session(columnName, provider, minimum, maximum, capacity, configuration));
    }

    private sealed class Session(
        string columnName,
        INationalIdentifierLocaleDataProvider provider,
        DateOnly minimum,
        DateOnly maximum,
        long capacity,
        NationalIdentifierGeneratorConfiguration configuration) : IGeneratorSession
    {
        private long _nextOrdinal;

        public ValueTask ApplyAsync(IGeneratorRow row, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!configuration.PreserveNulls || row.GetValue(columnName) is not null)
            {
                if (_nextOrdinal >= capacity)
                {
                    throw new InvalidOperationException(
                        $"National-identifier locale '{provider.Locale}' exhausted its {capacity:N0} configured values.");
                }

                GeneratedNationalIdentifier generated = provider.Generate(
                    _nextOrdinal,
                    configuration.Seed,
                    minimum,
                    maximum,
                    configuration.Gender);
                row.SetValue(columnName, generated.Value);
                _nextOrdinal++;
            }

            return ValueTask.CompletedTask;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
