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
        var requirements = new List<GeneratorDataRequirement>();
        if (!string.IsNullOrWhiteSpace(configuration.BirthDateColumn))
        {
            requirements.Add(new GeneratorDataRequirement("birth-date", binding.Table,
                [configuration.BirthDateColumn], configuration.BirthDateValueSource, false));
        }

        if (!string.IsNullOrWhiteSpace(configuration.GenderColumn))
        {
            requirements.Add(new GeneratorDataRequirement("gender", binding.Table,
                [configuration.GenderColumn], configuration.GenderValueSource, false));
        }

        return requirements;
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
        return ValueTask.FromResult<IGeneratorSession>(
            new Session(columnName, provider, minimum, maximum, configuration));
    }

    private sealed class Session(
        string columnName,
        INationalIdentifierLocaleDataProvider provider,
        DateOnly minimum,
        DateOnly maximum,
        NationalIdentifierGeneratorConfiguration configuration) : IGeneratorSession
    {
        private readonly Dictionary<(DateOnly? BirthDate, PersonGenderSelection Gender), long> _ordinals = new();

        public ValueTask ApplyAsync(IGeneratorRow row, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!configuration.PreserveNulls || row.GetValue(columnName) is not null)
            {
                DateOnly? sourcedBirthDate = ResolveBirthDate(row);
                PersonGenderSelection gender = ResolveGender(row);
                DateOnly effectiveMinimum = sourcedBirthDate ?? minimum;
                DateOnly effectiveMaximum = sourcedBirthDate ?? maximum;
                var key = provider.PartitionsIdentitySpaceByDemographicContext
                    ? (sourcedBirthDate, gender)
                    : (null, PersonGenderSelection.Any);
                _ordinals.TryGetValue(key, out long ordinal);
                long capacity = provider.GetCapacity(effectiveMinimum, effectiveMaximum, gender);
                if (ordinal >= capacity)
                {
                    throw new InvalidOperationException(
                        $"National-identifier locale '{provider.Locale}' exhausted its {capacity:N0} configured values.");
                }

                GeneratedNationalIdentifier generated = provider.Generate(
                    ordinal,
                    configuration.Seed,
                    effectiveMinimum,
                    effectiveMaximum,
                    gender);
                row.SetValue(columnName, generated.Value);
                _ordinals[key] = ordinal + 1;
            }

            return ValueTask.CompletedTask;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        private DateOnly? ResolveBirthDate(IGeneratorRow row)
        {
            if (string.IsNullOrWhiteSpace(configuration.BirthDateColumn))
            {
                return null;
            }

            object? value = row.GetValue(configuration.BirthDateColumn);
            return value switch
            {
                DateOnly date => date,
                DateTime dateTime => DateOnly.FromDateTime(dateTime),
                string text when NationalIdentifierGeneratorConfigurationCodec.TryParseDate(text, out DateOnly date) => date,
                null => throw new InvalidOperationException($"Birth-date column '{configuration.BirthDateColumn}' is null."),
                _ => throw new InvalidOperationException(
                    $"Birth-date column '{configuration.BirthDateColumn}' must contain a date or yyyy-MM-dd text.")
            };
        }

        private PersonGenderSelection ResolveGender(IGeneratorRow row)
        {
            if (string.IsNullOrWhiteSpace(configuration.GenderColumn))
            {
                return configuration.Gender;
            }

            string? value = row.GetValue(configuration.GenderColumn)?.ToString()?.Trim();
            if (value is null)
            {
                throw new InvalidOperationException($"Gender column '{configuration.GenderColumn}' is null.");
            }

            if (configuration.FemaleValues.Contains(value, StringComparer.OrdinalIgnoreCase))
            {
                return PersonGenderSelection.Female;
            }

            if (configuration.MaleValues.Contains(value, StringComparer.OrdinalIgnoreCase))
            {
                return PersonGenderSelection.Male;
            }

            throw new InvalidOperationException($"Gender value '{value}' is not mapped by the profile.");
        }
    }
}
