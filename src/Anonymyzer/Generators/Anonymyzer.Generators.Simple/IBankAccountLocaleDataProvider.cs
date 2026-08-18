namespace Anonymyzer.Generators.Simple;

public interface IBankAccountLocaleDataProvider
{
    string Locale { get; }

    long Capacity { get; }

    IReadOnlyList<string> Validate(BankAccountFormat format);

    string Generate(long ordinal, int seed, BankAccountFormat format);
}
