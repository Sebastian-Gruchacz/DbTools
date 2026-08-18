namespace Anonymyzer.Generators.Simple;

public sealed class CompanyNameGeneratorConfiguration
{
    public string Locale { get; set; } = "pl-PL";

    public string SyntheticMarker { get; set; } = "TEST";

    public bool IncludeLegalForm { get; set; } = true;

    public int Seed { get; set; }

    public bool PreserveNulls { get; set; } = true;
}
