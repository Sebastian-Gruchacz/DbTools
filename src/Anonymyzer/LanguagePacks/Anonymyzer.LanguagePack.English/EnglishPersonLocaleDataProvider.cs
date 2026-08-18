namespace Anonymyzer.LanguagePack.English;

using System.Globalization;
using System.Text;
using Anonymyzer.Generators.Person;

public sealed class EnglishPersonLocaleDataProvider : IPersonLocaleDataProvider
{
    private static readonly string[] FemaleNames =
    {
        "Olivia", "Emma", "Amelia", "Charlotte", "Mia", "Sophia", "Isabella", "Ava",
        "Evelyn", "Luna", "Camila", "Sofia", "Elizabeth", "Eleanor", "Harper", "Gianna"
    };

    private static readonly string[] MaleNames =
    {
        "Liam", "Noah", "Oliver", "Theodore", "James", "Henry", "Mateo", "Elijah",
        "Lucas", "William", "Benjamin", "Levi", "Sebastian", "Jack", "Daniel", "Alexander"
    };

    private static readonly string[] Surnames =
    {
        "Smith", "Johnson", "Williams", "Brown", "Jones", "Garcia", "Miller", "Davis",
        "Rodriguez", "Martinez", "Hernandez", "Lopez", "Gonzalez", "Wilson", "Anderson", "Thomas"
    };

    public string Locale => "en-US";

    public GeneratedPersonName GenerateName(Random random)
    {
        ArgumentNullException.ThrowIfNull(random);

        PersonGender gender = random.Next(2) == 0 ? PersonGender.Female : PersonGender.Male;
        string firstName = gender == PersonGender.Female
            ? FemaleNames[random.Next(FemaleNames.Length)]
            : MaleNames[random.Next(MaleNames.Length)];
        string lastName = Surnames[random.Next(Surnames.Length)];
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
