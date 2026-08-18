namespace Anonymyzer.LanguagePack.Polish;

using System.Globalization;
using Anonymyzer.Generators.Simple;

public sealed class PolishBankAccountLocaleDataProvider : IBankAccountLocaleDataProvider
{
    private const string NonRoutableBankAndBranch = "00000000";

    public string Locale => "pl-PL";

    public long Capacity => 10_000_000_000_000_000;

    public IReadOnlyList<string> Validate(BankAccountFormat format) =>
        Enum.IsDefined(format)
            ? Array.Empty<string>()
            : [$"Unsupported Polish bank-account format '{format}'."];

    public string Generate(long ordinal, int seed, BankAccountFormat format)
    {
        if (ordinal < 0 || ordinal >= Capacity)
        {
            throw new ArgumentOutOfRangeException(nameof(ordinal));
        }

        long accountOrdinal = ((uint)seed + ordinal) % Capacity;
        string bban = NonRoutableBankAndBranch
            + accountOrdinal.ToString("D16", CultureInfo.InvariantCulture);
        int checkDigits = 98 - CalculateMod97(bban + "252100");
        string iban = $"PL{checkDigits:D2}{bban}";

        return format switch
        {
            BankAccountFormat.IbanCompact => iban,
            BankAccountFormat.IbanGrouped => string.Join(' ', iban.Chunk(4).Select(chunk => new string(chunk))),
            BankAccountFormat.DomesticNrb => iban[2..],
            _ => throw new InvalidOperationException($"Unsupported Polish bank-account format '{format}'.")
        };
    }

    public static bool IsValidPolishIban(string value)
    {
        string compact = value.Replace(" ", string.Empty, StringComparison.Ordinal).ToUpperInvariant();
        return compact.Length == 28
            && compact.StartsWith("PL", StringComparison.Ordinal)
            && compact[2..].All(char.IsAsciiDigit)
            && CalculateMod97(compact[4..] + compact[..4]) == 1;
    }

    public static bool IsValidPolishNrb(string value)
    {
        string compact = value.Replace(" ", string.Empty, StringComparison.Ordinal);
        return compact.Length == 26
            && compact.All(char.IsAsciiDigit)
            && IsValidPolishIban("PL" + compact);
    }

    private static int CalculateMod97(string value)
    {
        int remainder = 0;
        foreach (char character in value)
        {
            if (char.IsAsciiDigit(character))
            {
                remainder = (remainder * 10 + character - '0') % 97;
                continue;
            }

            if (character is < 'A' or > 'Z')
            {
                return -1;
            }

            int numericValue = character - 'A' + 10;
            remainder = (remainder * 100 + numericValue) % 97;
        }

        return remainder;
    }
}
