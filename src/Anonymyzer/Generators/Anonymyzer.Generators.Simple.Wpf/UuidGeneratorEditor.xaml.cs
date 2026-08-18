namespace Anonymyzer.Generators.Simple.Wpf;

using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using Anonymyzer.ConfigEditor.Abstractions;
using Newtonsoft.Json.Linq;

public partial class UuidGeneratorEditor : UserControl, IGeneratorConfigurationEditor
{
    private readonly UuidGeneratorConfigurationCodec _codec = new();

    public UuidGeneratorEditor(JObject options)
    {
        InitializeComponent();
        FormatComboBox.ItemsSource = Enum.GetValues<UuidTextFormat>();

        var configuration = (UuidGeneratorConfiguration)_codec.Deserialize(options);
        SeedTextBox.Text = configuration.Seed;
        StartAtTextBox.Text = configuration.StartAt.ToString(CultureInfo.InvariantCulture);
        FormatComboBox.SelectedItem = configuration.Format;
        UppercaseCheckBox.IsChecked = configuration.Uppercase;
        PreserveNullsCheckBox.IsChecked = configuration.PreserveNulls;
    }

    public FrameworkElement View => this;

    public IReadOnlyList<string> Validate()
    {
        if (!TryBuildConfiguration(out UuidGeneratorConfiguration configuration, out List<string> errors))
        {
            return errors;
        }

        errors.AddRange(_codec.Validate(configuration));
        return errors;
    }

    public JObject Save()
    {
        if (!TryBuildConfiguration(out UuidGeneratorConfiguration configuration, out List<string> errors))
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
        out UuidGeneratorConfiguration configuration,
        out List<string> errors)
    {
        errors = new List<string>();
        if (!long.TryParse(StartAtTextBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out long startAt))
        {
            errors.Add("Start at must be an integer.");
        }

        UuidTextFormat format = FormatComboBox.SelectedItem is UuidTextFormat selectedFormat
            ? selectedFormat
            : default;
        if (FormatComboBox.SelectedItem is not UuidTextFormat)
        {
            errors.Add("Select a UUID format.");
        }

        configuration = new UuidGeneratorConfiguration
        {
            Seed = SeedTextBox.Text,
            StartAt = startAt,
            Format = format,
            Uppercase = UppercaseCheckBox.IsChecked == true,
            PreserveNulls = PreserveNullsCheckBox.IsChecked == true
        };
        return errors.Count == 0;
    }
}
