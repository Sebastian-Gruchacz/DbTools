namespace Anonymyzer.Generators.Person;

public sealed class NationalIdentifierGeneratorConfiguration
{
    public string Locale { get; set; } = "pl-PL";

    public string MinimumBirthDate { get; set; } = "1950-01-01";

    public string MaximumBirthDate { get; set; } = "2005-12-31";

    public PersonGenderSelection Gender { get; set; } = PersonGenderSelection.Any;

    public string BirthDateColumn { get; set; } = string.Empty;

    public Anonymyzer.Base.Generation.GeneratorValueSource BirthDateValueSource { get; set; } =
        Anonymyzer.Base.Generation.GeneratorValueSource.Original;

    public string GenderColumn { get; set; } = string.Empty;

    public Anonymyzer.Base.Generation.GeneratorValueSource GenderValueSource { get; set; } =
        Anonymyzer.Base.Generation.GeneratorValueSource.Original;

    public List<string> FemaleValues { get; set; } = ["F", "Female", "K", "Kobieta", "0"];

    public List<string> MaleValues { get; set; } = ["M", "Male", "Mężczyzna", "1"];

    public int Seed { get; set; } = 977;

    public bool PreserveNulls { get; set; } = true;
}
