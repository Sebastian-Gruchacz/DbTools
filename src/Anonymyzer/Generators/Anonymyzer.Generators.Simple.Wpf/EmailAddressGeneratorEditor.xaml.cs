namespace Anonymyzer.Generators.Simple.Wpf;

using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using Anonymyzer.Base.Generation;
using Anonymyzer.ConfigEditor.Abstractions;
using Newtonsoft.Json.Linq;

public partial class EmailAddressGeneratorEditor : UserControl, IGeneratorConfigurationEditor
{
    private readonly EmailAddressGeneratorConfigurationCodec _codec = new();

    public EmailAddressGeneratorEditor(JObject options)
    {
        InitializeComponent();
        PatternComboBox.ItemsSource = Enum.GetValues<EmailAddressPattern>();
        NameValueSourceComboBox.ItemsSource = Enum.GetValues<GeneratorValueSource>();

        var configuration = (EmailAddressGeneratorConfiguration)_codec.Deserialize(options);
        PatternComboBox.SelectedItem = configuration.Pattern;
        DomainTextBox.Text = configuration.Domain;
        OpaquePrefixTextBox.Text = configuration.OpaquePrefix;
        FirstNameColumnTextBox.Text = configuration.FirstNameColumn;
        LastNameColumnTextBox.Text = configuration.LastNameColumn;
        NameValueSourceComboBox.SelectedItem = configuration.NameValueSource;
        StartAtTextBox.Text = configuration.StartAt.ToString(CultureInfo.InvariantCulture);
        MinimumDigitsTextBox.Text = configuration.MinimumDigits.ToString(CultureInfo.InvariantCulture);
        PreserveNullsCheckBox.IsChecked = configuration.PreserveNulls;
    }

    public FrameworkElement View => this;

    public IReadOnlyList<string> Validate()
    {
        if (!TryBuildConfiguration(out EmailAddressGeneratorConfiguration configuration, out List<string> errors))
        {
            return errors;
        }

        errors.AddRange(_codec.Validate(configuration));
        return errors;
    }

    public JObject Save()
    {
        if (!TryBuildConfiguration(out EmailAddressGeneratorConfiguration configuration, out List<string> errors))
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
        out EmailAddressGeneratorConfiguration configuration,
        out List<string> errors)
    {
        errors = new List<string>();
        EmailAddressPattern pattern = PatternComboBox.SelectedItem is EmailAddressPattern selectedPattern
            ? selectedPattern
            : default;
        if (PatternComboBox.SelectedItem is not EmailAddressPattern)
        {
            errors.Add("Select an e-mail pattern.");
        }

        GeneratorValueSource nameValueSource = NameValueSourceComboBox.SelectedItem is GeneratorValueSource selectedSource
            ? selectedSource
            : default;
        if (NameValueSourceComboBox.SelectedItem is not GeneratorValueSource)
        {
            errors.Add("Select the name value source.");
        }

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

        configuration = new EmailAddressGeneratorConfiguration
        {
            Pattern = pattern,
            Domain = DomainTextBox.Text.Trim(),
            OpaquePrefix = OpaquePrefixTextBox.Text,
            FirstNameColumn = FirstNameColumnTextBox.Text.Trim(),
            LastNameColumn = LastNameColumnTextBox.Text.Trim(),
            NameValueSource = nameValueSource,
            StartAt = startAt,
            MinimumDigits = minimumDigits,
            PreserveNulls = PreserveNullsCheckBox.IsChecked == true
        };
        return errors.Count == 0;
    }
}
