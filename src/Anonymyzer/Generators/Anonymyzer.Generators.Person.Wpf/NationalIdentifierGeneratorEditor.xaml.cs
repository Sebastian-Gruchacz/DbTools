namespace Anonymyzer.Generators.Person.Wpf;

using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using Anonymyzer.Base.Generation;
using Anonymyzer.ConfigEditor.Abstractions;
using Newtonsoft.Json.Linq;

public partial class NationalIdentifierGeneratorEditor : UserControl, IGeneratorConfigurationEditor
{
    private readonly NationalIdentifierGeneratorConfigurationCodec _codec = new();

    public NationalIdentifierGeneratorEditor(JObject options)
    {
        InitializeComponent();
        LocaleComboBox.ItemsSource = new[] { "pl-PL", "en-US" };
        GenderComboBox.ItemsSource = Enum.GetValues<PersonGenderSelection>();
        BirthDateValueSourceComboBox.ItemsSource = Enum.GetValues<GeneratorValueSource>();
        GenderValueSourceComboBox.ItemsSource = Enum.GetValues<GeneratorValueSource>();
        var configuration = (NationalIdentifierGeneratorConfiguration)_codec.Deserialize(options);
        LocaleComboBox.Text = configuration.Locale;
        MinimumBirthDateTextBox.Text = configuration.MinimumBirthDate;
        MaximumBirthDateTextBox.Text = configuration.MaximumBirthDate;
        GenderComboBox.SelectedItem = configuration.Gender;
        BirthDateColumnTextBox.Text = configuration.BirthDateColumn;
        BirthDateValueSourceComboBox.SelectedItem = configuration.BirthDateValueSource;
        GenderColumnTextBox.Text = configuration.GenderColumn;
        GenderValueSourceComboBox.SelectedItem = configuration.GenderValueSource;
        FemaleValuesTextBox.Text = string.Join(", ", configuration.FemaleValues);
        MaleValuesTextBox.Text = string.Join(", ", configuration.MaleValues);
        SeedTextBox.Text = configuration.Seed.ToString(CultureInfo.InvariantCulture);
        PreserveNullsCheckBox.IsChecked = configuration.PreserveNulls;
    }

    public FrameworkElement View => this;

    public IReadOnlyList<string> Validate()
    {
        if (!TryBuild(out NationalIdentifierGeneratorConfiguration configuration, out List<string> errors))
        {
            return errors;
        }

        errors.AddRange(_codec.Validate(configuration));
        return errors;
    }

    public JObject Save()
    {
        if (!TryBuild(out NationalIdentifierGeneratorConfiguration configuration, out List<string> errors))
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, errors));
        }

        errors.AddRange(_codec.Validate(configuration));
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, errors));
        }

        return _codec.Serialize(configuration);
    }

    private bool TryBuild(
        out NationalIdentifierGeneratorConfiguration configuration,
        out List<string> errors)
    {
        errors = new List<string>();
        PersonGenderSelection gender = GenderComboBox.SelectedItem is PersonGenderSelection selectedGender
            ? selectedGender
            : default;
        if (GenderComboBox.SelectedItem is not PersonGenderSelection)
        {
            errors.Add("Select a gender.");
        }

        if (!int.TryParse(SeedTextBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int seed))
        {
            errors.Add("Seed must be an integer.");
        }

        configuration = new NationalIdentifierGeneratorConfiguration
        {
            Locale = LocaleComboBox.Text.Trim(),
            MinimumBirthDate = MinimumBirthDateTextBox.Text.Trim(),
            MaximumBirthDate = MaximumBirthDateTextBox.Text.Trim(),
            Gender = gender,
            BirthDateColumn = BirthDateColumnTextBox.Text.Trim(),
            BirthDateValueSource = BirthDateValueSourceComboBox.SelectedItem is GeneratorValueSource birthSource
                ? birthSource
                : default,
            GenderColumn = GenderColumnTextBox.Text.Trim(),
            GenderValueSource = GenderValueSourceComboBox.SelectedItem is GeneratorValueSource genderSource
                ? genderSource
                : default,
            FemaleValues = SplitValues(FemaleValuesTextBox.Text),
            MaleValues = SplitValues(MaleValuesTextBox.Text),
            Seed = seed,
            PreserveNulls = PreserveNullsCheckBox.IsChecked == true
        };
        return errors.Count == 0;
    }

    private static List<string> SplitValues(string value) => value.Split(',', StringSplitOptions.RemoveEmptyEntries)
        .Select(item => item.Trim()).Where(item => item.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
}
