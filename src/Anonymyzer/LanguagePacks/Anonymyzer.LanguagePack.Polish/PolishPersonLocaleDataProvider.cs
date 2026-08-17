namespace Anonymyzer.LanguagePack.Polish;

using System.Text;
using Anonymyzer.Generators.Person;

public sealed class PolishPersonLocaleDataProvider : IPersonLocaleDataProvider
{
    private static readonly string[] FemaleNames =
    {
        "Anna", "Katarzyna", "Maria", "Małgorzata", "Agnieszka", "Barbara", "Ewa", "Magdalena",
        "Joanna", "Aleksandra", "Monika", "Zofia", "Natalia", "Julia", "Karolina", "Marta"
    };

    private static readonly string[] MaleNames =
    {
        "Piotr", "Krzysztof", "Andrzej", "Tomasz", "Paweł", "Jan", "Michał", "Marcin",
        "Jakub", "Adam", "Łukasz", "Mateusz", "Wojciech", "Kamil", "Marek", "Grzegorz"
    };

    private static readonly SurnamePair[] Surnames =
    {
        new("Nowak", "Nowak"),
        new("Kowalski", "Kowalska"),
        new("Wiśniewski", "Wiśniewska"),
        new("Wójcik", "Wójcik"),
        new("Kowalczyk", "Kowalczyk"),
        new("Kamiński", "Kamińska"),
        new("Lewandowski", "Lewandowska"),
        new("Zieliński", "Zielińska"),
        new("Szymański", "Szymańska"),
        new("Woźniak", "Woźniak"),
        new("Dąbrowski", "Dąbrowska"),
        new("Kozłowski", "Kozłowska")
    };

    public string Locale => "pl-PL";

    public GeneratedPersonName GenerateName(Random random)
    {
        ArgumentNullException.ThrowIfNull(random);

        PersonGender gender = random.Next(2) == 0 ? PersonGender.Female : PersonGender.Male;
        string firstName = gender == PersonGender.Female
            ? FemaleNames[random.Next(FemaleNames.Length)]
            : MaleNames[random.Next(MaleNames.Length)];
        SurnamePair surname = Surnames[random.Next(Surnames.Length)];
        string lastName = gender == PersonGender.Female ? surname.Female : surname.Male;
        return new GeneratedPersonName(firstName, lastName, gender);
    }

    public string NormalizeEmailToken(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        var result = new StringBuilder(value.Length);
        foreach (char character in value.ToLowerInvariant())
        {
            char normalized = character switch
            {
                'ą' => 'a',
                'ć' => 'c',
                'ę' => 'e',
                'ł' => 'l',
                'ń' => 'n',
                'ó' => 'o',
                'ś' => 's',
                'ź' or 'ż' => 'z',
                _ => character
            };

            if (normalized is >= 'a' and <= 'z' or >= '0' and <= '9')
            {
                result.Append(normalized);
            }
        }

        return result.Length > 0
            ? result.ToString()
            : throw new InvalidOperationException($"Value '{value}' cannot be normalized to an e-mail token.");
    }

    private sealed record SurnamePair(string Male, string Female);
}
