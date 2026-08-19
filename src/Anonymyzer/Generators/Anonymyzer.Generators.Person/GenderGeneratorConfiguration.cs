namespace Anonymyzer.Generators.Person;

public sealed class GenderGeneratorConfiguration
{
    public string FemaleValue { get; set; } = "Female";

    public string MaleValue { get; set; } = "Male";

    public int FemalePercentage { get; set; } = 50;

    public int Seed { get; set; }

    public bool PreserveNulls { get; set; } = true;
}
