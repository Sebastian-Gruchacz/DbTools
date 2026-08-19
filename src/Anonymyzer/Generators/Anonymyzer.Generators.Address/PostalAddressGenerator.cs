namespace Anonymyzer.Generators.Address;

using Anonymyzer.Base;
using Anonymyzer.Base.Generation;

public sealed class PostalAddressGenerator : GeneratorBase<PostalAddressGeneratorConfiguration>
{
    public const string GeneratorType = "PostalAddress";
    public const string GeneratorVersion = "1.0.0";
    public const string CountryOutput = "Country";
    public const string RegionOutput = "Region";
    public const string CityOutput = "City";
    public const string StreetOutput = "Street";
    public const string PostalCodeOutput = "PostalCode";

    private static readonly GeneratorDescriptor GeneratorDescriptor = new(
        GeneratorType,
        GeneratorVersion,
        "Postal address",
        GeneratorExecutionScope.Row,
        DbDataType.Text)
    {
        SupportsDeterministicReplay = true,
        Outputs =
        [
            new GeneratorOutputDescriptor(CountryOutput, "Country", "Address.Country", Required: false),
            new GeneratorOutputDescriptor(RegionOutput, "Region", "Address.Region", Required: false),
            new GeneratorOutputDescriptor(CityOutput, "City", "Address.City", Required: false),
            new GeneratorOutputDescriptor(StreetOutput, "Street", "Address.Street", Required: false),
            new GeneratorOutputDescriptor(PostalCodeOutput, "Postal code", "Address.PostalCode", Required: false)
        ]
    };

    private static readonly PostalAddressGeneratorConfigurationCodec ConfigurationCodec = new();
    private readonly IReadOnlyDictionary<string, IPostalAddressLocaleDataProvider> _localeProviders;

    public PostalAddressGenerator(IEnumerable<IPostalAddressLocaleDataProvider> localeProviders)
    {
        ArgumentNullException.ThrowIfNull(localeProviders);
        _localeProviders = localeProviders.ToDictionary(provider => provider.Locale, StringComparer.OrdinalIgnoreCase);
    }

    public override GeneratorDescriptor Descriptor => GeneratorDescriptor;

    public override IGeneratorConfigurationCodec Configuration => ConfigurationCodec;

    protected override IReadOnlyList<GeneratorDataRequirement> GetDataRequirements(
        GeneratorBinding binding,
        PostalAddressGeneratorConfiguration configuration) => Array.Empty<GeneratorDataRequirement>();

    protected override ValueTask<IGeneratorSession> PrepareAsync(
        GeneratorPreparationContext context,
        PostalAddressGeneratorConfiguration configuration,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<string> errors = Configuration.Validate(configuration);
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, errors));
        }

        if (!_localeProviders.TryGetValue(configuration.Locale, out IPostalAddressLocaleDataProvider? provider))
        {
            throw new InvalidOperationException($"Postal-address locale '{configuration.Locale}' is not installed.");
        }

        if (context.Binding.Outputs.Count == 0)
        {
            throw new InvalidOperationException("PostalAddress requires at least one bound output.");
        }

        return ValueTask.FromResult<IGeneratorSession>(new Session(context.Binding, provider, configuration.Seed));
    }

    private sealed class Session(
        GeneratorBinding binding,
        IPostalAddressLocaleDataProvider provider,
        int seed) : IGeneratorSession
    {
        private readonly Random _random = new(seed);

        public ValueTask ApplyAsync(IGeneratorRow row, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            GeneratedPostalAddress address = provider.Generate(_random);
            SetIfBound(row, CountryOutput, address.Country);
            SetIfBound(row, RegionOutput, address.Region);
            SetIfBound(row, CityOutput, address.City);
            SetIfBound(row, StreetOutput, address.Street);
            SetIfBound(row, PostalCodeOutput, address.PostalCode);
            return ValueTask.CompletedTask;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        private void SetIfBound(IGeneratorRow row, string outputName, string value)
        {
            if (binding.TryGetOutput(outputName, out string columnName))
            {
                row.SetValue(columnName, value);
            }
        }
    }
}
