namespace Anonymyzer.Generators.Person;

using System.Globalization;
using Anonymyzer.Base.Generation;

public sealed class NationalIdentifierGeneratorConfigurationCodec
    : GeneratorConfigurationCodec<NationalIdentifierGeneratorConfiguration>
{
    public static bool TryParseDate(string? value, out DateOnly date) =>
        DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date);

    protected override NationalIdentifierGeneratorConfiguration CreateDefaultConfiguration() => new();

    protected override IEnumerable<string> ValidateConfiguration(NationalIdentifierGeneratorConfiguration configuration)
    {
        if (string.IsNullOrWhiteSpace(configuration.Locale))
        {
            yield return "Locale is required.";
        }

        bool hasMinimum = TryParseDate(configuration.MinimumBirthDate, out DateOnly minimum);
        bool hasMaximum = TryParseDate(configuration.MaximumBirthDate, out DateOnly maximum);
        if (!hasMinimum)
        {
            yield return "MinimumBirthDate must use yyyy-MM-dd.";
        }

        if (!hasMaximum)
        {
            yield return "MaximumBirthDate must use yyyy-MM-dd.";
        }

        if (hasMinimum && (minimum.Year < 1800 || minimum.Year > 2299))
        {
            yield return "MinimumBirthDate must be between 1800-01-01 and 2299-12-31.";
        }

        if (hasMaximum && (maximum.Year < 1800 || maximum.Year > 2299))
        {
            yield return "MaximumBirthDate must be between 1800-01-01 and 2299-12-31.";
        }

        if (hasMinimum && hasMaximum && minimum > maximum)
        {
            yield return "MinimumBirthDate cannot be later than MaximumBirthDate.";
        }

        if (!Enum.IsDefined(configuration.Gender))
        {
            yield return $"Unsupported gender selection '{configuration.Gender}'.";
        }

        if (!string.IsNullOrWhiteSpace(configuration.BirthDateColumn)
            && !Enum.IsDefined(configuration.BirthDateValueSource))
        {
            yield return $"Unsupported birth-date value source '{configuration.BirthDateValueSource}'.";
        }

        if (!string.IsNullOrWhiteSpace(configuration.GenderColumn))
        {
            if (!Enum.IsDefined(configuration.GenderValueSource))
            {
                yield return $"Unsupported gender value source '{configuration.GenderValueSource}'.";
            }

            if (configuration.FemaleValues is not { Count: > 0 }
                || configuration.MaleValues is not { Count: > 0 })
            {
                yield return "FemaleValues and MaleValues must not be empty when GenderColumn is configured.";
            }
            else if (configuration.FemaleValues.Intersect(
                         configuration.MaleValues,
                         StringComparer.OrdinalIgnoreCase).Any())
            {
                yield return "FemaleValues and MaleValues cannot overlap.";
            }
        }

        if (!string.IsNullOrWhiteSpace(configuration.BirthDateColumn)
            && configuration.BirthDateColumn.Equals(configuration.GenderColumn, StringComparison.OrdinalIgnoreCase))
        {
            yield return "BirthDateColumn and GenderColumn must be different.";
        }
    }
}
