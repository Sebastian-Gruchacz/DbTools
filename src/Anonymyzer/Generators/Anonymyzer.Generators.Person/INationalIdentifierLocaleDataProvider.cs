namespace Anonymyzer.Generators.Person;

public interface INationalIdentifierLocaleDataProvider
{
    string Locale { get; }

    bool PartitionsIdentitySpaceByDemographicContext { get; }

    long GetCapacity(DateOnly minimumBirthDate, DateOnly maximumBirthDate, PersonGenderSelection gender);

    GeneratedNationalIdentifier Generate(
        long ordinal,
        int seed,
        DateOnly minimumBirthDate,
        DateOnly maximumBirthDate,
        PersonGenderSelection gender);
}
