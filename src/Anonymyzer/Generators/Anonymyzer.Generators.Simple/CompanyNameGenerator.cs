namespace Anonymyzer.Generators.Simple;

using Anonymyzer.Base;
using Anonymyzer.Base.Generation;

public sealed class CompanyNameGenerator : GeneratorBase<CompanyNameGeneratorConfiguration>
{
    public const string GeneratorType = "CompanyName";
    public const string GeneratorVersion = "1.0.0";
    public const string ValueOutput = "Value";

    private static readonly GeneratorDescriptor GeneratorDescriptor = new(
        GeneratorType,
        GeneratorVersion,
        "Company name",
        GeneratorExecutionScope.Row,
        DbDataType.Text)
    {
        Outputs = [new GeneratorOutputDescriptor(ValueOutput, "Company name", "Company.Name", Required: true)]
    };

    private static readonly CompanyNameGeneratorConfigurationCodec ConfigurationCodec = new();
    private readonly IReadOnlyDictionary<string, ICompanyNameLocaleDataProvider> _localeProviders;

    public CompanyNameGenerator(IEnumerable<ICompanyNameLocaleDataProvider> localeProviders)
    {
        ArgumentNullException.ThrowIfNull(localeProviders);
        _localeProviders = localeProviders.ToDictionary(provider => provider.Locale, StringComparer.OrdinalIgnoreCase);
    }

    public override GeneratorDescriptor Descriptor => GeneratorDescriptor;

    public override IGeneratorConfigurationCodec Configuration => ConfigurationCodec;

    protected override IReadOnlyList<GeneratorDataRequirement> GetDataRequirements(
        GeneratorBinding binding,
        CompanyNameGeneratorConfiguration configuration)
    {
        binding.GetRequiredOutput(ValueOutput);
        return Array.Empty<GeneratorDataRequirement>();
    }

    protected override ValueTask<IGeneratorSession> PrepareAsync(
        GeneratorPreparationContext context,
        CompanyNameGeneratorConfiguration configuration,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<string> errors = Configuration.Validate(configuration);
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, errors));
        }

        if (!_localeProviders.TryGetValue(configuration.Locale, out ICompanyNameLocaleDataProvider? provider))
        {
            throw new InvalidOperationException($"Company-name locale '{configuration.Locale}' is not installed.");
        }

        return ValueTask.FromResult<IGeneratorSession>(new Session(
            context.Binding.GetRequiredOutput(ValueOutput),
            provider,
            configuration));
    }

    private sealed class Session(
        string columnName,
        ICompanyNameLocaleDataProvider provider,
        CompanyNameGeneratorConfiguration configuration) : IGeneratorSession
    {
        private readonly Random _random = new(configuration.Seed);
        private long _sequence;

        public ValueTask ApplyAsync(IGeneratorRow row, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!configuration.PreserveNulls || row.GetValue(columnName) is not null)
            {
                _sequence++;
                row.SetValue(columnName, provider.Generate(
                    _random,
                    _sequence,
                    configuration.SyntheticMarker.Trim(),
                    configuration.IncludeLegalForm));
            }

            return ValueTask.CompletedTask;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
