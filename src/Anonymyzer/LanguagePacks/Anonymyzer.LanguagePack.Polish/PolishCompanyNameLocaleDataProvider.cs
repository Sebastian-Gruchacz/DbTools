namespace Anonymyzer.LanguagePack.Polish;

using System.Globalization;
using Anonymyzer.Generators.Simple;

public sealed class PolishCompanyNameLocaleDataProvider : ICompanyNameLocaleDataProvider
{
    private static readonly string[] Names = ["Bursztyn", "Horyzont", "Latarnia", "Puls", "Sygnał", "Wektor"];
    private static readonly string[] Sectors = ["Systemy", "Logistyka", "Technologie", "Usługi", "Rozwiązania", "Handel"];
    private static readonly string[] LegalForms = ["sp. z o.o.", "S.A.", "sp.k."];

    public string Locale => "pl-PL";

    public string Generate(Random random, long sequence, string syntheticMarker, bool includeLegalForm)
    {
        ArgumentNullException.ThrowIfNull(random);
        string legalForm = includeLegalForm ? $" {LegalForms[random.Next(LegalForms.Length)]}" : string.Empty;
        return $"{Names[random.Next(Names.Length)]} {Sectors[random.Next(Sectors.Length)]} "
               + $"{syntheticMarker} {sequence.ToString("D6", CultureInfo.InvariantCulture)}{legalForm}";
    }
}
