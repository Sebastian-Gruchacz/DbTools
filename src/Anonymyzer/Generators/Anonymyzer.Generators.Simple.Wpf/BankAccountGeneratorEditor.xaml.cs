namespace Anonymyzer.Generators.Simple.Wpf;

using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using Anonymyzer.ConfigEditor.Abstractions;
using Newtonsoft.Json.Linq;

public partial class BankAccountGeneratorEditor : UserControl, IGeneratorConfigurationEditor
{
    private readonly BankAccountGeneratorConfigurationCodec _codec = new();

    public BankAccountGeneratorEditor(JObject options)
    {
        InitializeComponent();
        LocaleComboBox.ItemsSource = new[] { "pl-PL" };
        FormatComboBox.ItemsSource = Enum.GetValues<BankAccountFormat>();

        var configuration = (BankAccountGeneratorConfiguration)_codec.Deserialize(options);
        LocaleComboBox.Text = configuration.Locale;
        FormatComboBox.SelectedItem = configuration.Format;
        SeedTextBox.Text = configuration.Seed.ToString(CultureInfo.InvariantCulture);
        PreserveNullsCheckBox.IsChecked = configuration.PreserveNulls;
    }

    public FrameworkElement View => this;

    public IReadOnlyList<string> Validate()
    {
        if (!TryBuildConfiguration(out BankAccountGeneratorConfiguration configuration, out List<string> errors))
        {
            return errors;
        }

        errors.AddRange(_codec.Validate(configuration));
        return errors;
    }

    public JObject Save()
    {
        if (!TryBuildConfiguration(out BankAccountGeneratorConfiguration configuration, out List<string> errors))
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
        out BankAccountGeneratorConfiguration configuration,
        out List<string> errors)
    {
        errors = new List<string>();
        BankAccountFormat format = FormatComboBox.SelectedItem is BankAccountFormat selectedFormat
            ? selectedFormat
            : default;
        if (FormatComboBox.SelectedItem is not BankAccountFormat)
        {
            errors.Add("Select a bank-account format.");
        }

        if (!int.TryParse(SeedTextBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int seed))
        {
            errors.Add("Seed must be an integer.");
        }

        configuration = new BankAccountGeneratorConfiguration
        {
            Locale = LocaleComboBox.Text.Trim(),
            Format = format,
            Seed = seed,
            PreserveNulls = PreserveNullsCheckBox.IsChecked == true
        };
        return errors.Count == 0;
    }
}
