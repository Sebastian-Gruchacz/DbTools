namespace Anonymyzer.LanguagePack.Polish;

using System.Globalization;
using Anonymyzer.Generators.Simple;

public sealed class PolishTaxIdentifierLocaleDataProvider : ITaxIdentifierLocaleDataProvider
{
    private static readonly int[] Weights = [6, 5, 7, 2, 3, 4, 5, 6, 7];
    private static readonly int[] Regon9Weights = [8, 9, 2, 3, 4, 5, 6, 7];
    private static readonly int[] Regon14Weights = [2, 4, 8, 5, 0, 9, 7, 3, 6, 1, 2, 4, 8];

    public string Locale => "pl-PL";

    public long GetCapacity(string variant) => variant.ToUpperInvariant() switch
    {
        "NIP" => 810_000_000,
        "REGON9" => 90_000_000,
        "REGON14" => 900_000_000_000,
        _ => 0
    };

    public IReadOnlyList<string> Validate(string variant, TaxIdentifierFormat format)
    {
        if (GetCapacity(variant) == 0)
        {
            return [$"Unsupported Polish tax/registry identifier variant '{variant}'."];
        }

        return !variant.Equals("NIP", StringComparison.OrdinalIgnoreCase) && format != TaxIdentifierFormat.DigitsOnly
            ? ["REGON variants support only DigitsOnly format."]
            : Array.Empty<string>();
    }

    public string Generate(long ordinal, int seed, string variant, TaxIdentifierFormat format)
    {
        return variant.ToUpperInvariant() switch
        {
            "NIP" => GenerateNip(ordinal, seed, format),
            "REGON9" => GenerateRegon9(ordinal, seed),
            "REGON14" => GenerateRegon14(ordinal, seed),
            _ => throw new InvalidOperationException($"Unsupported Polish identifier variant '{variant}'.")
        };
    }

    private string GenerateNip(long ordinal, int seed, TaxIdentifierFormat format)
    {
        long index = ((uint)seed + ordinal) % GetCapacity("NIP");
        int firstEight = checked(10_000_000 + (int)(index / 9));
        int choice = (int)(index % 9);
        Span<int> digits = stackalloc int[10];
        WriteEightDigits(firstEight, digits);
        int partial = 0;
        for (int position = 0; position < 8; position++)
        {
            partial += digits[position] * Weights[position];
        }

        digits[8] = SelectValidNinthDigit(partial, choice);
        digits[9] = (partial + digits[8] * Weights[8]) % 11;
        string value = string.Concat(digits.ToArray().Select(digit => digit.ToString(CultureInfo.InvariantCulture)));
        return format switch
        {
            TaxIdentifierFormat.DigitsOnly => value,
            TaxIdentifierFormat.Hyphenated => $"{value[..3]}-{value[3..6]}-{value[6..8]}-{value[8..]}",
            TaxIdentifierFormat.International => $"PL{value}",
            _ => throw new InvalidOperationException($"Unsupported Polish NIP format '{format}'.")
        };
    }

    private string GenerateRegon9(long ordinal, int seed)
    {
        long index = ((uint)seed + ordinal) % GetCapacity("REGON9");
        string prefix = (10_000_000 + index).ToString("D8", CultureInfo.InvariantCulture);
        return prefix + CalculateChecksum(prefix, Regon9Weights);
    }

    private string GenerateRegon14(long ordinal, int seed)
    {
        long index = ((uint)seed + ordinal) % GetCapacity("REGON14");
        long entityIndex = index / 10_000;
        string regon9 = GenerateRegon9(entityIndex, 0);
        string prefix = regon9 + (index % 10_000).ToString("D4", CultureInfo.InvariantCulture);
        return prefix + CalculateChecksum(prefix, Regon14Weights);
    }

    private static int CalculateChecksum(string prefix, IReadOnlyList<int> weights)
    {
        int checksum = prefix.Select((character, index) => (character - '0') * weights[index]).Sum() % 11;
        return checksum == 10 ? 0 : checksum;
    }

    public static bool IsValidRegon(string value)
    {
        if (value.Any(character => !char.IsAsciiDigit(character)))
        {
            return false;
        }

        return value.Length switch
        {
            9 => CalculateChecksum(value[..8], Regon9Weights) == value[8] - '0',
            14 => IsValidRegon(value[..9])
                && CalculateChecksum(value[..13], Regon14Weights) == value[13] - '0',
            _ => false
        };
    }

    public static bool IsValidNip(string value)
    {
        if (value.Length != 10 || value.Any(character => !char.IsAsciiDigit(character)))
        {
            return false;
        }

        int sum = 0;
        for (int position = 0; position < Weights.Length; position++)
        {
            sum += (value[position] - '0') * Weights[position];
        }

        int checksum = sum % 11;
        return checksum != 10 && checksum == value[9] - '0';
    }

    private static void WriteEightDigits(int value, Span<int> digits)
    {
        for (int position = 7; position >= 0; position--)
        {
            digits[position] = value % 10;
            value /= 10;
        }
    }

    private static int SelectValidNinthDigit(int partial, int choice)
    {
        for (int digit = 0; digit <= 9; digit++)
        {
            if ((partial + digit * Weights[8]) % 11 == 10)
            {
                continue;
            }

            if (choice-- == 0)
            {
                return digit;
            }
        }

        throw new InvalidOperationException("Could not select a valid NIP checksum source digit.");
    }
}
