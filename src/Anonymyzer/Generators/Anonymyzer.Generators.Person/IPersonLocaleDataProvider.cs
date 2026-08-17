namespace Anonymyzer.Generators.Person;

public interface IPersonLocaleDataProvider
{
    string Locale { get; }

    GeneratedPersonName GenerateName(Random random);

    string NormalizeEmailToken(string value);
}
