namespace Anonymyzer.Generators.Person.Wpf;

using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using Anonymyzer.ConfigEditor.Abstractions;
using Newtonsoft.Json.Linq;

public partial class PersonIdentityGeneratorEditor : UserControl, IGeneratorConfigurationEditor
{
    private readonly PersonIdentityGeneratorConfigurationCodec _codec = new();

    public PersonIdentityGeneratorEditor(JObject options)
    {
        InitializeComponent();

        var configuration = (PersonIdentityGeneratorConfiguration)_codec.Deserialize(options);
        LocaleComboBox.ItemsSource = new[] { "pl-PL", "en-US" };
        EmailPatternComboBox.ItemsSource = Enum.GetValues<PersonEmailPattern>();
        SeedTextBox.Text = configuration.Seed.ToString(CultureInfo.InvariantCulture);
        LocaleComboBox.Text = configuration.Locale;
        EmailPatternComboBox.SelectedItem = configuration.EmailPattern;
        EmailDomainTextBox.Text = configuration.EmailDomain;
    }

    public FrameworkElement View => this;

    public IReadOnlyList<string> Validate()
    {
        if (!TryBuildConfiguration(out PersonIdentityGeneratorConfiguration configuration, out List<string> errors))
        {
            return errors;
        }

        errors.AddRange(_codec.Validate(configuration));
        return errors;
    }

    public JObject Save()
    {
        if (!TryBuildConfiguration(out PersonIdentityGeneratorConfiguration configuration, out List<string> errors))
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
        out PersonIdentityGeneratorConfiguration configuration,
        out List<string> errors)
    {
        errors = new List<string>();
        if (!int.TryParse(SeedTextBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int seed))
        {
            errors.Add("Seed must be an integer.");
        }

        if (EmailPatternComboBox.SelectedItem is not PersonEmailPattern emailPattern)
        {
            errors.Add("E-mail pattern is required.");
            emailPattern = PersonEmailPattern.NameBased;
        }

        configuration = new PersonIdentityGeneratorConfiguration
        {
            Seed = seed,
            Locale = LocaleComboBox.Text.Trim(),
            EmailPattern = emailPattern,
            EmailDomain = EmailDomainTextBox.Text.Trim()
        };
        return errors.Count == 0;
    }
}
