namespace Anonymyzer.LanguagePack.English;

using System.Globalization;
using Anonymyzer.Generators.Simple;

public sealed class EnglishCompanyNameLocaleDataProvider : ICompanyNameLocaleDataProvider
{
    private static readonly string[] Names = ["Amber", "Horizon", "Beacon", "Pulse", "Signal", "Vector"];
    private static readonly string[] Sectors = ["Systems", "Logistics", "Technologies", "Services", "Solutions", "Trading"];
    private static readonly string[] LegalForms = ["LLC", "Inc.", "Ltd."];

    public string Locale => "en-US";

    public string Generate(Random random, long sequence, string syntheticMarker, bool includeLegalForm)
    {
        ArgumentNullException.ThrowIfNull(random);
        string legalForm = includeLegalForm ? $" {LegalForms[random.Next(LegalForms.Length)]}" : string.Empty;
        return $"{Names[random.Next(Names.Length)]} {Sectors[random.Next(Sectors.Length)]} "
               + $"{syntheticMarker} {sequence.ToString("D6", CultureInfo.InvariantCulture)}{legalForm}";
    }
}
