namespace Anonymyzer.Generators.Simple;

public sealed class BankAccountGeneratorConfiguration
{
    public string Locale { get; set; } = "pl-PL";

    public BankAccountFormat Format { get; set; } = BankAccountFormat.IbanCompact;

    public int Seed { get; set; } = 593;

    public bool PreserveNulls { get; set; } = true;
}
