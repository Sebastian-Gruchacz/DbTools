namespace Anonymyzer.Generators.Simple.Wpf;

using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using Anonymyzer.ConfigEditor.Abstractions;
using Newtonsoft.Json.Linq;

public partial class TaxIdentifierGeneratorEditor : UserControl, IGeneratorConfigurationEditor
{
    private readonly TaxIdentifierGeneratorConfigurationCodec _codec = new();

    public TaxIdentifierGeneratorEditor(JObject options)
    {
        InitializeComponent();
        LocaleComboBox.ItemsSource = new[] { "pl-PL" };
        FormatComboBox.ItemsSource = Enum.GetValues<TaxIdentifierFormat>();

        var configuration = (TaxIdentifierGeneratorConfiguration)_codec.Deserialize(options);
        LocaleComboBox.Text = configuration.Locale;
        FormatComboBox.SelectedItem = configuration.Format;
        SeedTextBox.Text = configuration.Seed.ToString(CultureInfo.InvariantCulture);
        PreserveNullsCheckBox.IsChecked = configuration.PreserveNulls;
    }

    public FrameworkElement View => this;

    public IReadOnlyList<string> Validate()
    {
        if (!TryBuildConfiguration(out TaxIdentifierGeneratorConfiguration configuration, out List<string> errors))
        {
            return errors;
        }

        errors.AddRange(_codec.Validate(configuration));
        return errors;
    }

    public JObject Save()
    {
        if (!TryBuildConfiguration(out TaxIdentifierGeneratorConfiguration configuration, out List<string> errors))
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
        out TaxIdentifierGeneratorConfiguration configuration,
        out List<string> errors)
    {
        errors = new List<string>();
        TaxIdentifierFormat format = FormatComboBox.SelectedItem is TaxIdentifierFormat selectedFormat
            ? selectedFormat
            : default;
        if (FormatComboBox.SelectedItem is not TaxIdentifierFormat)
        {
            errors.Add("Select a tax-identifier format.");
        }

        if (!int.TryParse(SeedTextBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int seed))
        {
            errors.Add("Seed must be an integer.");
        }

        configuration = new TaxIdentifierGeneratorConfiguration
        {
            Locale = LocaleComboBox.Text.Trim(),
            Format = format,
            Seed = seed,
            PreserveNulls = PreserveNullsCheckBox.IsChecked == true
        };
        return errors.Count == 0;
    }
}
