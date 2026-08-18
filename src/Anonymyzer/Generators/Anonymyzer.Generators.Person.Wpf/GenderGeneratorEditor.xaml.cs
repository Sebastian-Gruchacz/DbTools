namespace Anonymyzer.Generators.Person.Wpf;

using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using Anonymyzer.ConfigEditor.Abstractions;
using Newtonsoft.Json.Linq;

public partial class GenderGeneratorEditor : UserControl, IGeneratorConfigurationEditor
{
    private readonly GenderGeneratorConfigurationCodec _codec = new();

    public GenderGeneratorEditor(JObject options)
    {
        InitializeComponent();
        var configuration = (GenderGeneratorConfiguration)_codec.Deserialize(options);
        FemaleValueTextBox.Text = configuration.FemaleValue;
        MaleValueTextBox.Text = configuration.MaleValue;
        FemalePercentageTextBox.Text = configuration.FemalePercentage.ToString(CultureInfo.InvariantCulture);
        SeedTextBox.Text = configuration.Seed.ToString(CultureInfo.InvariantCulture);
        PreserveNullsCheckBox.IsChecked = configuration.PreserveNulls;
    }

    public FrameworkElement View => this;

    public IReadOnlyList<string> Validate()
    {
        if (!TryBuild(out GenderGeneratorConfiguration configuration, out List<string> errors))
        {
            return errors;
        }

        errors.AddRange(_codec.Validate(configuration));
        return errors;
    }

    public JObject Save()
    {
        if (!TryBuild(out GenderGeneratorConfiguration configuration, out List<string> errors))
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

    private bool TryBuild(out GenderGeneratorConfiguration configuration, out List<string> errors)
    {
        errors = new List<string>();
        if (!int.TryParse(FemalePercentageTextBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture,
                out int femalePercentage))
        {
            errors.Add("Female percentage must be an integer.");
        }

        if (!int.TryParse(SeedTextBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int seed))
        {
            errors.Add("Seed must be an integer.");
        }

        configuration = new GenderGeneratorConfiguration
        {
            FemaleValue = FemaleValueTextBox.Text.Trim(),
            MaleValue = MaleValueTextBox.Text.Trim(),
            FemalePercentage = femalePercentage,
            Seed = seed,
            PreserveNulls = PreserveNullsCheckBox.IsChecked == true
        };
        return errors.Count == 0;
    }
}
