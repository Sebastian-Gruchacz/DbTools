namespace Anonymyzer.Generators.Person.Wpf;

using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using Anonymyzer.ConfigEditor.Abstractions;
using Newtonsoft.Json.Linq;

public partial class BirthDateGeneratorEditor : UserControl, IGeneratorConfigurationEditor
{
    private readonly BirthDateGeneratorConfigurationCodec _codec = new();

    public BirthDateGeneratorEditor(JObject options)
    {
        InitializeComponent();
        var configuration = (BirthDateGeneratorConfiguration)_codec.Deserialize(options);
        MinimumDateTextBox.Text = configuration.MinimumDate;
        MaximumDateTextBox.Text = configuration.MaximumDate;
        SeedTextBox.Text = configuration.Seed.ToString(CultureInfo.InvariantCulture);
        PreserveNullsCheckBox.IsChecked = configuration.PreserveNulls;
    }

    public FrameworkElement View => this;

    public IReadOnlyList<string> Validate()
    {
        if (!TryBuild(out BirthDateGeneratorConfiguration configuration, out List<string> errors))
        {
            return errors;
        }

        errors.AddRange(_codec.Validate(configuration));
        return errors;
    }

    public JObject Save()
    {
        if (!TryBuild(out BirthDateGeneratorConfiguration configuration, out List<string> errors))
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

    private bool TryBuild(out BirthDateGeneratorConfiguration configuration, out List<string> errors)
    {
        errors = new List<string>();
        if (!int.TryParse(SeedTextBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int seed))
        {
            errors.Add("Seed must be an integer.");
        }

        configuration = new BirthDateGeneratorConfiguration
        {
            MinimumDate = MinimumDateTextBox.Text.Trim(),
            MaximumDate = MaximumDateTextBox.Text.Trim(),
            Seed = seed,
            PreserveNulls = PreserveNullsCheckBox.IsChecked == true
        };
        return errors.Count == 0;
    }
}
