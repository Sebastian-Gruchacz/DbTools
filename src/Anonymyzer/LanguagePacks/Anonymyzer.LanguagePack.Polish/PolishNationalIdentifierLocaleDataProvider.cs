namespace Anonymyzer.LanguagePack.Polish;

using System.Globalization;
using Anonymyzer.Generators.Person;

public sealed class PolishNationalIdentifierLocaleDataProvider : INationalIdentifierLocaleDataProvider
{
    private static readonly int[] ChecksumWeights = [1, 3, 7, 9, 1, 3, 7, 9, 1, 3];

    public string Locale => "pl-PL";

    public bool PartitionsIdentitySpaceByDemographicContext => true;

    public long GetCapacity(DateOnly minimumBirthDate, DateOnly maximumBirthDate, PersonGenderSelection gender)
    {
        long days = maximumBirthDate.DayNumber - minimumBirthDate.DayNumber + 1L;
        return days * GetSerialCapacity(gender);
    }

    public GeneratedNationalIdentifier Generate(
        long ordinal,
        int seed,
        DateOnly minimumBirthDate,
        DateOnly maximumBirthDate,
        PersonGenderSelection gender)
    {
        long capacity = GetCapacity(minimumBirthDate, maximumBirthDate, gender);
        long index = ((uint)seed + ordinal) % capacity;
        int serialCapacity = GetSerialCapacity(gender);
        DateOnly birthDate = minimumBirthDate.AddDays((int)(index / serialCapacity));
        int serial = BuildSerial((int)(index % serialCapacity), gender);
        PersonGender actualGender = serial % 2 == 0 ? PersonGender.Female : PersonGender.Male;

        Span<int> digits = stackalloc int[11];
        digits[0] = birthDate.Year % 100 / 10;
        digits[1] = birthDate.Year % 10;
        int encodedMonth = birthDate.Month + GetCenturyOffset(birthDate.Year);
        digits[2] = encodedMonth / 10;
        digits[3] = encodedMonth % 10;
        digits[4] = birthDate.Day / 10;
        digits[5] = birthDate.Day % 10;
        digits[6] = serial / 1000;
        digits[7] = serial / 100 % 10;
        digits[8] = serial / 10 % 10;
        digits[9] = serial % 10;

        int sum = 0;
        for (int position = 0; position < ChecksumWeights.Length; position++)
        {
            sum += digits[position] * ChecksumWeights[position];
        }

        digits[10] = (10 - sum % 10) % 10;
        string value = string.Concat(digits.ToArray().Select(digit => digit.ToString(CultureInfo.InvariantCulture)));
        return new GeneratedNationalIdentifier(value, birthDate, actualGender);
    }

    public static bool IsValidPesel(string value)
    {
        if (value.Length != 11 || value.Any(character => !char.IsAsciiDigit(character)))
        {
            return false;
        }

        int sum = 0;
        for (int position = 0; position < ChecksumWeights.Length; position++)
        {
            sum += (value[position] - '0') * ChecksumWeights[position];
        }

        return (10 - sum % 10) % 10 == value[10] - '0' && TryDecodeBirthDate(value, out _);
    }

    public static bool TryDecodeBirthDate(string value, out DateOnly birthDate)
    {
        birthDate = default;
        if (value.Length < 6 || value.Take(6).Any(character => !char.IsAsciiDigit(character)))
        {
            return false;
        }

        int year = (value[0] - '0') * 10 + value[1] - '0';
        int encodedMonth = (value[2] - '0') * 10 + value[3] - '0';
        int day = (value[4] - '0') * 10 + value[5] - '0';
        int century = encodedMonth switch
        {
            >= 81 and <= 92 => 1800,
            >= 1 and <= 12 => 1900,
            >= 21 and <= 32 => 2000,
            >= 41 and <= 52 => 2100,
            >= 61 and <= 72 => 2200,
            _ => 0
        };
        if (century == 0)
        {
            return false;
        }

        int month = encodedMonth % 20;
        return DateOnly.TryParseExact(
            $"{century + year:D4}-{month:D2}-{day:D2}",
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out birthDate);
    }

    private static int GetSerialCapacity(PersonGenderSelection gender) =>
        gender == PersonGenderSelection.Any ? 10_000 : 5_000;

    private static int BuildSerial(int index, PersonGenderSelection gender)
    {
        if (gender == PersonGenderSelection.Any)
        {
            return index;
        }

        int genderDigit = index % 5 * 2 + (gender == PersonGenderSelection.Male ? 1 : 0);
        return index / 5 * 10 + genderDigit;
    }

    private static int GetCenturyOffset(int year) => year switch
    {
        < 1900 => 80,
        < 2000 => 0,
        < 2100 => 20,
        < 2200 => 40,
        _ => 60
    };
}
