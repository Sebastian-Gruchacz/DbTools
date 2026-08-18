namespace Anonymyzer.Generators.Person.Wpf;

using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using Anonymyzer.ConfigEditor.Abstractions;
using Newtonsoft.Json.Linq;

public partial class NationalIdentifierGeneratorEditor : UserControl, IGeneratorConfigurationEditor
{
    private readonly NationalIdentifierGeneratorConfigurationCodec _codec = new();

    public NationalIdentifierGeneratorEditor(JObject options)
    {
        InitializeComponent();
        LocaleComboBox.ItemsSource = new[] { "pl-PL" };
        GenderComboBox.ItemsSource = Enum.GetValues<PersonGenderSelection>();
        var configuration = (NationalIdentifierGeneratorConfiguration)_codec.Deserialize(options);
        LocaleComboBox.Text = configuration.Locale;
        MinimumBirthDateTextBox.Text = configuration.MinimumBirthDate;
        MaximumBirthDateTextBox.Text = configuration.MaximumBirthDate;
        GenderComboBox.SelectedItem = configuration.Gender;
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
            Seed = seed,
            PreserveNulls = PreserveNullsCheckBox.IsChecked == true
        };
        return errors.Count == 0;
    }
}
