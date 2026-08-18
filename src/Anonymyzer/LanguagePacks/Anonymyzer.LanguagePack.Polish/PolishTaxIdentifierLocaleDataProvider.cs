namespace Anonymyzer.LanguagePack.Polish;

using System.Globalization;
using Anonymyzer.Generators.Simple;

public sealed class PolishTaxIdentifierLocaleDataProvider : ITaxIdentifierLocaleDataProvider
{
    private static readonly int[] Weights = [6, 5, 7, 2, 3, 4, 5, 6, 7];

    public string Locale => "pl-PL";

    public long Capacity => 810_000_000;

    public string Generate(long ordinal, int seed, TaxIdentifierFormat format)
    {
        long index = ((uint)seed + ordinal) % Capacity;
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
