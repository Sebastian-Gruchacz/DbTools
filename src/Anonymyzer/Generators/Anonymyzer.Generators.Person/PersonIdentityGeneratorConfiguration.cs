namespace Anonymyzer.Generators.Person;

public sealed class PersonIdentityGeneratorConfiguration
{
    public int Seed { get; set; }

    public string Locale { get; set; } = "pl-PL";

    public PersonFullNamePattern FullNamePattern { get; set; } = PersonFullNamePattern.FirstNameLastName;

    public PersonEmailPattern EmailPattern { get; set; } = PersonEmailPattern.NameBased;

    public string EmailDomain { get; set; } = "example.invalid";
}
