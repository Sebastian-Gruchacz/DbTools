namespace Anonymyzer.LanguagePack.English;

using System.Globalization;
using System.Text;
using Anonymyzer.Base.Generation;
using Anonymyzer.Generators.Person;

public sealed class EnglishPersonLocaleDataProvider : IPersonLocaleDataProvider
{
    public const string GivenNameDataVersion = "US-SSA-2020-2025";
    public const string SurnameDataVersion = "US-Census-2010";

    private static readonly WeightedRandomTable<string> FemaleNames = new(
    [
        new("Olivia", 95_853), new("Emma", 85_652), new("Charlotte", 78_055), new("Amelia", 76_108),
        new("Sophia", 74_758), new("Mia", 68_154), new("Isabella", 67_643), new("Ava", 63_350),
        new("Evelyn", 55_734), new("Harper", 47_509), new("Luna", 46_161), new("Camila", 45_999),
        new("Sofia", 45_288), new("Eleanor", 41_966), new("Elizabeth", 41_893), new("Gianna", 39_710)
    ]);

    private static readonly WeightedRandomTable<string> MaleNames = new(
    [
        new("Liam", 124_842), new("Noah", 116_024), new("Oliver", 89_293), new("James", 72_523),
        new("Elijah", 72_049), new("William", 68_067), new("Henry", 68_047), new("Lucas", 65_795),
        new("Theodore", 65_579), new("Benjamin", 64_922), new("Mateo", 62_272), new("Levi", 57_122),
        new("Sebastian", 53_454), new("Jack", 53_384), new("Daniel", 52_726), new("Michael", 52_631)
    ]);

    private static readonly WeightedRandomTable<string> Surnames = new(
    [
        new("Smith", 2_442_977), new("Johnson", 1_932_812), new("Williams", 1_625_252),
        new("Brown", 1_437_026), new("Jones", 1_425_470), new("Garcia", 1_166_120),
        new("Miller", 1_161_437), new("Davis", 1_116_357), new("Rodriguez", 1_094_924),
        new("Martinez", 1_060_159), new("Hernandez", 1_043_281), new("Lopez", 874_523),
        new("Gonzalez", 841_025), new("Wilson", 801_882), new("Anderson", 784_404),
        new("Thomas", 756_142)
    ]);

    public string Locale => "en-US";

    public GeneratedPersonName GenerateName(Random random)
    {
        ArgumentNullException.ThrowIfNull(random);

        PersonGender gender = random.Next(2) == 0 ? PersonGender.Female : PersonGender.Male;
        string firstName = gender == PersonGender.Female
            ? FemaleNames.Select(random)
            : MaleNames.Select(random);
        string lastName = Surnames.Select(random);
        return new GeneratedPersonName(firstName, lastName, gender);
    }

    public string NormalizeEmailToken(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        var result = new StringBuilder(value.Length);
        foreach (char character in value.Normalize(NormalizationForm.FormD).ToLowerInvariant())
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark
                && character is >= 'a' and <= 'z' or >= '0' and <= '9')
            {
                result.Append(character);
            }
        }

        return result.Length > 0
            ? result.ToString()
            : throw new InvalidOperationException($"Value '{value}' cannot be normalized to an e-mail token.");
    }
}
