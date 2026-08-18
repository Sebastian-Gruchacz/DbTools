namespace Anonymyzer.Generators.Simple;

public interface ICompanyNameLocaleDataProvider
{
    string Locale { get; }

    string Generate(Random random, long sequence, string syntheticMarker, bool includeLegalForm);
}
