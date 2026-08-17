namespace Anonymyzer.Generators.Simple.Wpf;

using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using Anonymyzer.ConfigEditor.Abstractions;
using Newtonsoft.Json.Linq;

public partial class SequentialTextGeneratorEditor : UserControl, IGeneratorConfigurationEditor
{
    private readonly SequentialTextGeneratorConfigurationCodec _codec = new();

    public SequentialTextGeneratorEditor(JObject options)
    {
        InitializeComponent();
        var configuration = (SequentialTextGeneratorConfiguration)_codec.Deserialize(options);
        PrefixTextBox.Text = configuration.Prefix;
        SuffixTextBox.Text = configuration.Suffix;
        StartAtTextBox.Text = configuration.StartAt.ToString(CultureInfo.InvariantCulture);
        MinimumDigitsTextBox.Text = configuration.MinimumDigits.ToString(CultureInfo.InvariantCulture);
        PreserveNullsCheckBox.IsChecked = configuration.PreserveNulls;
    }

    public FrameworkElement View => this;

    public IReadOnlyList<string> Validate()
    {
        if (!TryBuildConfiguration(out SequentialTextGeneratorConfiguration configuration, out List<string> errors))
        {
            return errors;
        }

        errors.AddRange(_codec.Validate(configuration));
        return errors;
    }

    public JObject Save()
    {
        if (!TryBuildConfiguration(out SequentialTextGeneratorConfiguration configuration, out List<string> errors))
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
        out SequentialTextGeneratorConfiguration configuration,
        out List<string> errors)
    {
        errors = new List<string>();
        if (!long.TryParse(StartAtTextBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out long startAt))
        {
            errors.Add("Start at must be an integer.");
        }

        if (!int.TryParse(
                MinimumDigitsTextBox.Text,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out int minimumDigits))
        {
            errors.Add("Minimum digits must be an integer.");
        }

        configuration = new SequentialTextGeneratorConfiguration
        {
            Prefix = PrefixTextBox.Text,
            Suffix = SuffixTextBox.Text,
            StartAt = startAt,
            MinimumDigits = minimumDigits,
            PreserveNulls = PreserveNullsCheckBox.IsChecked == true
        };
        return errors.Count == 0;
    }
}
