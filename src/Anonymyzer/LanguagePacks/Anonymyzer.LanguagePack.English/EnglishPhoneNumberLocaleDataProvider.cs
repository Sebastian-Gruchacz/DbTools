namespace Anonymyzer.LanguagePack.English;

using Anonymyzer.Generators.Simple;

public sealed class EnglishPhoneNumberLocaleDataProvider : IPhoneNumberLocaleDataProvider
{
    public string Locale => "en-US";

    public long Capacity => 100;

    public string Generate(long ordinal, int seed, PhoneNumberFormat format)
    {
        long lineNumber = 100 + ((uint)seed + ordinal) % Capacity;
        string national = $"(202) 555-{lineNumber:D4}";
        return format == PhoneNumberFormat.International
            ? $"+1 202-555-{lineNumber:D4}"
            : national;
    }
}
