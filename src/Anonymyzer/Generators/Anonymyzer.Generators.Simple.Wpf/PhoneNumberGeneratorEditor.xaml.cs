namespace Anonymyzer.Generators.Simple.Wpf;

using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using Anonymyzer.ConfigEditor.Abstractions;
using Newtonsoft.Json.Linq;

public partial class PhoneNumberGeneratorEditor : UserControl, IGeneratorConfigurationEditor
{
    private readonly PhoneNumberGeneratorConfigurationCodec _codec = new();

    public PhoneNumberGeneratorEditor(JObject options)
    {
        InitializeComponent();
        LocaleComboBox.ItemsSource = new[] { "pl-PL", "en-US" };
        FormatComboBox.ItemsSource = Enum.GetValues<PhoneNumberFormat>();

        var configuration = (PhoneNumberGeneratorConfiguration)_codec.Deserialize(options);
        LocaleComboBox.Text = configuration.Locale;
        FormatComboBox.SelectedItem = configuration.Format;
        SeedTextBox.Text = configuration.Seed.ToString(CultureInfo.InvariantCulture);
        PreserveNullsCheckBox.IsChecked = configuration.PreserveNulls;
    }

    public FrameworkElement View => this;

    public IReadOnlyList<string> Validate()
    {
        if (!TryBuildConfiguration(out PhoneNumberGeneratorConfiguration configuration, out List<string> errors))
        {
            return errors;
        }

        errors.AddRange(_codec.Validate(configuration));
        return errors;
    }

    public JObject Save()
    {
        if (!TryBuildConfiguration(out PhoneNumberGeneratorConfiguration configuration, out List<string> errors))
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

    private bool TryBuildConfiguration(
        out PhoneNumberGeneratorConfiguration configuration,
        out List<string> errors)
    {
        errors = new List<string>();
        PhoneNumberFormat format = FormatComboBox.SelectedItem is PhoneNumberFormat selectedFormat
            ? selectedFormat
            : default;
        if (FormatComboBox.SelectedItem is not PhoneNumberFormat)
        {
            errors.Add("Select a phone-number format.");
        }

        if (!int.TryParse(SeedTextBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int seed))
        {
            errors.Add("Seed must be an integer.");
        }

        configuration = new PhoneNumberGeneratorConfiguration
        {
            Locale = LocaleComboBox.Text.Trim(),
            Format = format,
            Seed = seed,
            PreserveNulls = PreserveNullsCheckBox.IsChecked == true
        };
        return errors.Count == 0;
    }
}
