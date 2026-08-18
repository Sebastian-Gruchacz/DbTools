namespace Anonymyzer.Generators.Person;

using System.Globalization;
using Anonymyzer.Base.Generation;

public sealed class BirthDateGeneratorConfigurationCodec
    : GeneratorConfigurationCodec<BirthDateGeneratorConfiguration>
{
    public static bool TryParseDate(string? value, out DateOnly date) =>
        DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date);

    protected override BirthDateGeneratorConfiguration CreateDefaultConfiguration() => new();

    protected override IEnumerable<string> ValidateConfiguration(BirthDateGeneratorConfiguration configuration)
    {
        bool hasMinimum = TryParseDate(configuration.MinimumDate, out DateOnly minimum);
        bool hasMaximum = TryParseDate(configuration.MaximumDate, out DateOnly maximum);
        if (!hasMinimum)
        {
            yield return "MinimumDate must use yyyy-MM-dd.";
        }

        if (!hasMaximum)
        {
            yield return "MaximumDate must use yyyy-MM-dd.";
        }

        if (hasMinimum && hasMaximum && minimum > maximum)
        {
            yield return "MinimumDate cannot be later than MaximumDate.";
        }
    }
}
