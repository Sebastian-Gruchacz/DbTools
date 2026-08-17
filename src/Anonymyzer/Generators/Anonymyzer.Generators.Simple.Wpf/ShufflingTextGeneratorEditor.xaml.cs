namespace Anonymyzer.Generators.Simple.Wpf;

using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using Anonymyzer.ConfigEditor.Abstractions;
using Newtonsoft.Json.Linq;

public partial class ShufflingTextGeneratorEditor : UserControl, IGeneratorConfigurationEditor
{
    private readonly ShufflingTextGeneratorConfigurationCodec _codec = new();

    public ShufflingTextGeneratorEditor(JObject options)
    {
        InitializeComponent();

        var configuration = (ShufflingTextGeneratorConfiguration)_codec.Deserialize(options);
        SeedTextBox.Text = configuration.Seed.ToString(CultureInfo.InvariantCulture);
        MinimumPopulationTextBox.Text = configuration.MinimumPopulation.ToString(CultureInfo.InvariantCulture);
        PreserveNullsCheckBox.IsChecked = configuration.PreserveNulls;
    }

    public FrameworkElement View => this;

    public IReadOnlyList<string> Validate()
    {
        if (!TryBuildConfiguration(out ShufflingTextGeneratorConfiguration configuration, out List<string> errors))
        {
            return errors;
        }

        errors.AddRange(_codec.Validate(configuration));
        return errors;
    }

    public JObject Save()
    {
        if (!TryBuildConfiguration(out ShufflingTextGeneratorConfiguration configuration, out List<string> errors))
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
        out ShufflingTextGeneratorConfiguration configuration,
        out List<string> errors)
    {
        errors = new List<string>();
        if (!int.TryParse(SeedTextBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int seed))
        {
            errors.Add("Seed must be an integer.");
        }

        if (!int.TryParse(MinimumPopulationTextBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int minimumPopulation))
        {
            errors.Add("Minimum population must be an integer.");
        }

        configuration = new ShufflingTextGeneratorConfiguration
        {
            Seed = seed,
            MinimumPopulation = minimumPopulation,
            PreserveNulls = PreserveNullsCheckBox.IsChecked == true
        };
        return errors.Count == 0;
    }
}
