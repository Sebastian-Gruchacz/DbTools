namespace Anonymyzer.Generators.Person;

using Anonymyzer.Base;
using Anonymyzer.Base.Generation;

public sealed class PersonIdentityGenerator : GeneratorBase<PersonIdentityGeneratorConfiguration>
{
    public const string GeneratorType = "PersonIdentity";
    public const string GeneratorVersion = "1.1.0";
    public const string FirstNameOutput = "FirstName";
    public const string LastNameOutput = "LastName";
    public const string GenderOutput = "Gender";
    public const string EmailOutput = "Email";

    private static readonly GeneratorDescriptor GeneratorDescriptor = new(
        GeneratorType,
        GeneratorVersion,
        "Person identity",
        GeneratorExecutionScope.Row,
        DbDataType.Text)
    {
        SupportsDeterministicReplay = true,
        Outputs = new[]
        {
            new GeneratorOutputDescriptor(FirstNameOutput, "First name", "Person.FirstName", Required: false),
            new GeneratorOutputDescriptor(LastNameOutput, "Last name", "Person.LastName", Required: false),
            new GeneratorOutputDescriptor(GenderOutput, "Gender", "Person.Gender", Required: false),
            new GeneratorOutputDescriptor(EmailOutput, "E-mail", "Contact.Email", Required: false)
        }
    };

    private static readonly PersonIdentityGeneratorConfigurationCodec ConfigurationCodec = new();
    private readonly IReadOnlyDictionary<string, IPersonLocaleDataProvider> _localeProviders;

    public PersonIdentityGenerator(IEnumerable<IPersonLocaleDataProvider> localeProviders)
    {
        ArgumentNullException.ThrowIfNull(localeProviders);
        _localeProviders = localeProviders.ToDictionary(provider => provider.Locale, StringComparer.OrdinalIgnoreCase);
    }

    public override GeneratorDescriptor Descriptor => GeneratorDescriptor;

    public override IGeneratorConfigurationCodec Configuration => ConfigurationCodec;

    protected override IReadOnlyList<GeneratorDataRequirement> GetDataRequirements(
        GeneratorBinding binding,
        PersonIdentityGeneratorConfiguration configuration)
    {
        return Array.Empty<GeneratorDataRequirement>();
    }

    protected override ValueTask<IGeneratorSession> PrepareAsync(
        GeneratorPreparationContext context,
        PersonIdentityGeneratorConfiguration configuration,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<string> errors = Configuration.Validate(configuration);
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, errors));
        }

        if (!_localeProviders.TryGetValue(configuration.Locale, out IPersonLocaleDataProvider? localeProvider))
        {
            throw new InvalidOperationException($"Person locale '{configuration.Locale}' is not installed.");
        }

        if (context.Binding.Outputs.Count == 0)
        {
            throw new InvalidOperationException("PersonIdentity requires at least one bound output.");
        }

        return ValueTask.FromResult<IGeneratorSession>(new PersonIdentityGeneratorSession(
            context.Binding,
            localeProvider,
            configuration));
    }

    private sealed class PersonIdentityGeneratorSession(
        GeneratorBinding binding,
        IPersonLocaleDataProvider localeProvider,
        PersonIdentityGeneratorConfiguration configuration) : IGeneratorSession
    {
        private readonly Random _random = new(configuration.Seed);
        private long _sequence;

        public ValueTask ApplyAsync(IGeneratorRow row, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            GeneratedPersonName person = localeProvider.GenerateName(_random);
            _sequence++;

            SetIfBound(row, FirstNameOutput, person.FirstName);
            SetIfBound(row, LastNameOutput, person.LastName);
            SetIfBound(row, GenderOutput, person.Gender.ToString());
            SetIfBound(row, EmailOutput, BuildEmail(person));
            return ValueTask.CompletedTask;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        private string BuildEmail(GeneratedPersonName person)
        {
            string localPart = configuration.EmailPattern switch
            {
                PersonEmailPattern.NameBased => string.Join(
                    '.',
                    localeProvider.NormalizeEmailToken(person.FirstName),
                    localeProvider.NormalizeEmailToken(person.LastName),
                    _sequence.ToString("D6", System.Globalization.CultureInfo.InvariantCulture)),
                PersonEmailPattern.Opaque => $"person.{_sequence.ToString("D8", System.Globalization.CultureInfo.InvariantCulture)}",
                _ => throw new InvalidOperationException($"Unsupported e-mail pattern {configuration.EmailPattern}.")
            };

            return $"{localPart}@{configuration.EmailDomain.ToLowerInvariant()}";
        }

        private void SetIfBound(IGeneratorRow row, string outputName, object value)
        {
            if (binding.TryGetOutput(outputName, out string columnName))
            {
                row.SetValue(columnName, value);
            }
        }
    }
}
