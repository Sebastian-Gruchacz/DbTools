namespace Anonymyzer.Generators.Address.Wpf;

using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using Anonymyzer.ConfigEditor.Abstractions;
using Newtonsoft.Json.Linq;

public partial class PostalAddressGeneratorEditor : UserControl, IGeneratorConfigurationEditor
{
    private readonly PostalAddressGeneratorConfigurationCodec _codec = new();

    public PostalAddressGeneratorEditor(JObject options)
    {
        InitializeComponent();
        LocaleComboBox.ItemsSource = new[] { "pl-PL", "en-US" };
        var configuration = (PostalAddressGeneratorConfiguration)_codec.Deserialize(options);
        LocaleComboBox.Text = configuration.Locale;
        SeedTextBox.Text = configuration.Seed.ToString(CultureInfo.InvariantCulture);
    }

    public FrameworkElement View => this;

    public IReadOnlyList<string> Validate()
    {
        if (!TryBuild(out PostalAddressGeneratorConfiguration configuration, out List<string> errors))
        {
            return errors;
        }

        errors.AddRange(_codec.Validate(configuration));
        return errors;
    }

    public JObject Save()
    {
        if (!TryBuild(out PostalAddressGeneratorConfiguration configuration, out List<string> errors))
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

    private bool TryBuild(out PostalAddressGeneratorConfiguration configuration, out List<string> errors)
    {
        errors = new List<string>();
        if (!int.TryParse(SeedTextBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int seed))
        {
            errors.Add("Seed must be an integer.");
        }

        configuration = new PostalAddressGeneratorConfiguration
        {
            Locale = LocaleComboBox.Text.Trim(),
            Seed = seed
        };
        return errors.Count == 0;
    }
}
