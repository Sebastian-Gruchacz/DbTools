namespace Anonymyzer.Generators.Simple;

public interface ITaxIdentifierLocaleDataProvider
{
    string Locale { get; }

    long GetCapacity(string variant);

    IReadOnlyList<string> Validate(string variant, TaxIdentifierFormat format);

    string Generate(long ordinal, int seed, string variant, TaxIdentifierFormat format);
}
