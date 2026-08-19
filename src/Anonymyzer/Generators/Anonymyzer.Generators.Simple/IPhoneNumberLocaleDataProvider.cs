namespace Anonymyzer.Generators.Simple;

public interface IPhoneNumberLocaleDataProvider
{
    string Locale { get; }

    long Capacity { get; }

    string Generate(long ordinal, int seed, PhoneNumberFormat format);
}
