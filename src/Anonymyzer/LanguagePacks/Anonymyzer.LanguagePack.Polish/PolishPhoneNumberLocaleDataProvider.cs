namespace Anonymyzer.LanguagePack.Polish;

using Anonymyzer.Generators.Simple;

public sealed class PolishPhoneNumberLocaleDataProvider : IPhoneNumberLocaleDataProvider
{
    public string Locale => "pl-PL";

    public long Capacity => 1_000_000;

    public string Generate(long ordinal, int seed, PhoneNumberFormat format)
    {
        long subscriber = ((uint)seed + ordinal) % Capacity;
        string national = $"501 {subscriber / 1000:D3} {subscriber % 1000:D3}";
        return format == PhoneNumberFormat.International ? $"+48 {national}" : national;
    }
}
