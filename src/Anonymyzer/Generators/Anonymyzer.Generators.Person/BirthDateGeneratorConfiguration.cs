namespace Anonymyzer.Generators.Person;

public sealed class BirthDateGeneratorConfiguration
{
    public string MinimumDate { get; set; } = "1950-01-01";

    public string MaximumDate { get; set; } = "2005-12-31";

    public int Seed { get; set; }

    public bool PreserveNulls { get; set; } = true;
}
