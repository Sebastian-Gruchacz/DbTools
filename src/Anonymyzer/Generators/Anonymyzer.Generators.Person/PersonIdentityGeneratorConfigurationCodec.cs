namespace Anonymyzer.Generators.Person;

using Anonymyzer.Base.Generation;

public sealed class PersonIdentityGeneratorConfigurationCodec
    : GeneratorConfigurationCodec<PersonIdentityGeneratorConfiguration>
{
    protected override PersonIdentityGeneratorConfiguration CreateDefaultConfiguration()
    {
        return new PersonIdentityGeneratorConfiguration();
    }

    protected override IEnumerable<string> ValidateConfiguration(PersonIdentityGeneratorConfiguration configuration)
    {
        if (string.IsNullOrWhiteSpace(configuration.Locale))
        {
            yield return "Locale is required.";
        }

        if (string.IsNullOrWhiteSpace(configuration.EmailDomain)
            || !configuration.EmailDomain.Contains('.', StringComparison.Ordinal)
            || configuration.EmailDomain.Contains('@', StringComparison.Ordinal)
            || configuration.EmailDomain.Any(char.IsWhiteSpace))
        {
            yield return "EmailDomain must be a plain DNS name, for example example.invalid.";
        }
    }
}
