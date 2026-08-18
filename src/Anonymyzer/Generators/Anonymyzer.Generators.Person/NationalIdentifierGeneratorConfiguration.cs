namespace Anonymyzer.Generators.Person;

public sealed class NationalIdentifierGeneratorConfiguration
{
    public string Locale { get; set; } = "pl-PL";

    public string MinimumBirthDate { get; set; } = "1950-01-01";

    public string MaximumBirthDate { get; set; } = "2005-12-31";

    public PersonGenderSelection Gender { get; set; } = PersonGenderSelection.Any;

    public int Seed { get; set; } = 977;

    public bool PreserveNulls { get; set; } = true;
}
