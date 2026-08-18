namespace Anonymyzer.Generators.Simple.Wpf;

using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using Anonymyzer.Base.Generation;
using Anonymyzer.ConfigEditor.Abstractions;
using Newtonsoft.Json.Linq;

public partial class AccountLoginGeneratorEditor : UserControl, IGeneratorConfigurationEditor
{
    private readonly AccountLoginGeneratorConfigurationCodec _codec = new();

    public AccountLoginGeneratorEditor(JObject options)
    {
        InitializeComponent();
        PatternComboBox.ItemsSource = Enum.GetValues<AccountLoginPattern>();
        NameValueSourceComboBox.ItemsSource = Enum.GetValues<GeneratorValueSource>();
        var configuration = (AccountLoginGeneratorConfiguration)_codec.Deserialize(options);
        PatternComboBox.SelectedItem = configuration.Pattern;
        OpaquePrefixTextBox.Text = configuration.OpaquePrefix;
        FirstNameColumnTextBox.Text = configuration.FirstNameColumn;
        LastNameColumnTextBox.Text = configuration.LastNameColumn;
        NameValueSourceComboBox.SelectedItem = configuration.NameValueSource;
        SeparatorTextBox.Text = configuration.Separator;
        StartAtTextBox.Text = configuration.StartAt.ToString(CultureInfo.InvariantCulture);
        MinimumDigitsTextBox.Text = configuration.MinimumDigits.ToString(CultureInfo.InvariantCulture);
        PreserveNullsCheckBox.IsChecked = configuration.PreserveNulls;
    }

    public FrameworkElement View => this;
    public IReadOnlyList<string> Validate() => TryBuild(out var value, out var errors)
        ? errors.Concat(_codec.Validate(value)).ToArray() : errors;

    public JObject Save()
    {
        if (!TryBuild(out AccountLoginGeneratorConfiguration value, out List<string> errors))
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, errors));
        }

        errors.AddRange(_codec.Validate(value));
        return errors.Count == 0 ? _codec.Serialize(value) : throw new InvalidOperationException(string.Join(Environment.NewLine, errors));
    }

    private bool TryBuild(out AccountLoginGeneratorConfiguration value, out List<string> errors)
    {
        errors = new List<string>();
        var pattern = PatternComboBox.SelectedItem is AccountLoginPattern selectedPattern ? selectedPattern : default;
        var source = NameValueSourceComboBox.SelectedItem is GeneratorValueSource selectedSource ? selectedSource : default;
        if (PatternComboBox.SelectedItem is null) errors.Add("Select a login pattern.");
        if (NameValueSourceComboBox.SelectedItem is null) errors.Add("Select the name value source.");
        if (!long.TryParse(StartAtTextBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out long startAt)) errors.Add("Start must be an integer.");
        if (!int.TryParse(MinimumDigitsTextBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int digits)) errors.Add("Digits must be an integer.");
        value = new AccountLoginGeneratorConfiguration
        {
            Pattern = pattern, OpaquePrefix = OpaquePrefixTextBox.Text,
            FirstNameColumn = FirstNameColumnTextBox.Text.Trim(), LastNameColumn = LastNameColumnTextBox.Text.Trim(),
            NameValueSource = source, Separator = SeparatorTextBox.Text, StartAt = startAt, MinimumDigits = digits,
            PreserveNulls = PreserveNullsCheckBox.IsChecked == true
        };
        return errors.Count == 0;
    }
}
