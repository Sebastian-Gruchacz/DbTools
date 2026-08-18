namespace Anonymyzer.LanguagePack.English;

using Anonymyzer.Generators.Person;

public sealed class EnglishNationalIdentifierLocaleDataProvider : INationalIdentifierLocaleDataProvider
{
    public const long SafeValueCapacity = 1_000_000;

    public string Locale => "en-US";

    public bool PartitionsIdentitySpaceByDemographicContext => false;

    public long GetCapacity(DateOnly minimumBirthDate, DateOnly maximumBirthDate, PersonGenderSelection gender) =>
        SafeValueCapacity;

    public GeneratedNationalIdentifier Generate(
        long ordinal,
        int seed,
        DateOnly minimumBirthDate,
        DateOnly maximumBirthDate,
        PersonGenderSelection gender)
    {
        long index = ((uint)seed + ordinal) % SafeValueCapacity;
        int group = (int)(index / 10_000);
        int serial = (int)(index % 10_000);
        int dayCount = maximumBirthDate.DayNumber - minimumBirthDate.DayNumber + 1;
        DateOnly birthDate = minimumBirthDate.AddDays((int)(index % dayCount));
        PersonGender actualGender = gender switch
        {
            PersonGenderSelection.Female => PersonGender.Female,
            PersonGenderSelection.Male => PersonGender.Male,
            _ => index % 2 == 0 ? PersonGender.Female : PersonGender.Male
        };

        return new GeneratedNationalIdentifier($"000-{group:D2}-{serial:D4}", birthDate, actualGender);
    }
}
