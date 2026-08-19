namespace Anonymyzer.Generators.Simple.Wpf;

using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using Anonymyzer.ConfigEditor.Abstractions;
using Newtonsoft.Json.Linq;

public partial class CompanyNameGeneratorEditor : UserControl, IGeneratorConfigurationEditor
{
    private readonly CompanyNameGeneratorConfigurationCodec _codec = new();

    public CompanyNameGeneratorEditor(JObject options)
    {
        InitializeComponent();
        LocaleComboBox.ItemsSource = new[] { "pl-PL", "en-US" };
        var configuration = (CompanyNameGeneratorConfiguration)_codec.Deserialize(options);
        LocaleComboBox.Text = configuration.Locale;
        SyntheticMarkerTextBox.Text = configuration.SyntheticMarker;
        SeedTextBox.Text = configuration.Seed.ToString(CultureInfo.InvariantCulture);
        IncludeLegalFormCheckBox.IsChecked = configuration.IncludeLegalForm;
        PreserveNullsCheckBox.IsChecked = configuration.PreserveNulls;
    }

    public FrameworkElement View => this;

    public IReadOnlyList<string> Validate()
    {
        if (!TryBuild(out CompanyNameGeneratorConfiguration configuration, out List<string> errors))
        {
            return errors;
        }

        errors.AddRange(_codec.Validate(configuration));
        return errors;
    }

    public JObject Save()
    {
        if (!TryBuild(out CompanyNameGeneratorConfiguration configuration, out List<string> errors))
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

    private bool TryBuild(out CompanyNameGeneratorConfiguration configuration, out List<string> errors)
    {
        errors = new List<string>();
        if (!int.TryParse(SeedTextBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int seed))
        {
            errors.Add("Seed must be an integer.");
        }

        configuration = new CompanyNameGeneratorConfiguration
        {
            Locale = LocaleComboBox.Text.Trim(),
            SyntheticMarker = SyntheticMarkerTextBox.Text.Trim(),
            IncludeLegalForm = IncludeLegalFormCheckBox.IsChecked == true,
            Seed = seed,
            PreserveNulls = PreserveNullsCheckBox.IsChecked == true
        };
        return errors.Count == 0;
    }
}
