namespace Anonymyzer.Generators.Simple;

public interface ITaxIdentifierLocaleDataProvider
{
    string Locale { get; }

    long Capacity { get; }

    string Generate(long ordinal, int seed, TaxIdentifierFormat format);
}
