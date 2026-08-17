namespace Anonymyzer.Generators.Simple;

public sealed class PhoneNumberGeneratorConfiguration
{
    public string Locale { get; set; } = "pl-PL";

    public PhoneNumberFormat Format { get; set; } = PhoneNumberFormat.International;

    public int Seed { get; set; } = 173;

    public bool PreserveNulls { get; set; } = true;
}
