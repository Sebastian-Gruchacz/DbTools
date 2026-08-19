namespace Anonymyzer.LanguagePack.Polish;

using System.Text;
using Anonymyzer.Base.Generation;
using Anonymyzer.Generators.Person;

public sealed class PolishPersonLocaleDataProvider : IPersonLocaleDataProvider
{
    public const string GivenNameDataVersion = "PL-MC-2025";
    public const string SurnameDataVersion = "PL-PESEL-2022-published-ranking";

    private static readonly WeightedRandomTable<string> FemaleNames = new(
    [
        new("Zofia", 4_415), new("Zuzanna", 3_881), new("Maja", 3_873), new("Laura", 3_821),
        new("Hanna", 3_452), new("Julia", 3_207), new("Oliwia", 3_067), new("Pola", 2_812),
        new("Alicja", 2_788), new("Emilia", 2_556)
    ]);

    private static readonly WeightedRandomTable<string> MaleNames = new(
    [
        new("Nikodem", 5_772), new("Antoni", 5_253), new("Leon", 5_079), new("Jan", 5_054),
        new("Aleksander", 4_687), new("Franciszek", 4_548), new("Ignacy", 4_222),
        new("Stanisław", 3_554), new("Jakub", 3_386), new("Mikołaj", 3_215)
    ]);

    private static readonly WeightedRandomTable<SurnamePair> Surnames = new(
    [
        Pair("Nowak", "Nowak", 202_132),
        Pair("Kowalski", "Kowalska", 136_545),
        Pair("Wiśniewski", "Wiśniewska", 108_977),
        Pair("Wójcik", "Wójcik", 98_128),
        Pair("Kowalczyk", "Kowalczyk", 96_814),
        Pair("Kamiński", "Kamińska", 93_961),
        Pair("Lewandowski", "Lewandowska", 92_142),
        Pair("Zieliński", "Zielińska", 88_839),
        Pair("Szymański", "Szymańska", 87_507),
        Pair("Woźniak", "Woźniak", 87_173),
        Pair("Dąbrowski", "Dąbrowska", 86_274)
    ]);

    public string Locale => "pl-PL";

    public GeneratedPersonName GenerateName(Random random)
    {
        ArgumentNullException.ThrowIfNull(random);

        PersonGender gender = random.Next(2) == 0 ? PersonGender.Female : PersonGender.Male;
        string firstName = gender == PersonGender.Female
            ? FemaleNames.Select(random)
            : MaleNames.Select(random);
        SurnamePair surname = Surnames.Select(random);
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

    private static WeightedValue<SurnamePair> Pair(string male, string female, long weight) =>
        new(new SurnamePair(male, female), weight);

    private sealed record SurnamePair(string Male, string Female);
}
