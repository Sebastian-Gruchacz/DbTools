namespace Anonymyzer.Generators.Simple;

public sealed class TaxIdentifierGeneratorConfiguration
{
    public string Locale { get; set; } = "pl-PL";

    public string Variant { get; set; } = "NIP";

    public TaxIdentifierFormat Format { get; set; } = TaxIdentifierFormat.DigitsOnly;

    public int Seed { get; set; } = 431;

    public bool PreserveNulls { get; set; } = true;
}
