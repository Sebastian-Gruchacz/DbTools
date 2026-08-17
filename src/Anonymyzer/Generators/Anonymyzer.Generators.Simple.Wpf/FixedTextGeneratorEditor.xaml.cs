namespace Anonymyzer.Generators.Simple.Wpf;

using System.Windows;
using System.Windows.Controls;
using Anonymyzer.ConfigEditor.Abstractions;
using Newtonsoft.Json.Linq;

public partial class FixedTextGeneratorEditor : UserControl, IGeneratorConfigurationEditor
{
    private readonly FixedTextGeneratorConfigurationCodec _codec = new();

    public FixedTextGeneratorEditor(JObject options)
    {
        InitializeComponent();
        var configuration = (FixedTextGeneratorConfiguration)_codec.Deserialize(options);
        ValueTextBox.Text = configuration.Value;
        PreserveNullsCheckBox.IsChecked = configuration.PreserveNulls;
    }

    public FrameworkElement View => this;

    public IReadOnlyList<string> Validate() => _codec.Validate(BuildConfiguration());

    public JObject Save()
    {
        FixedTextGeneratorConfiguration configuration = BuildConfiguration();
        IReadOnlyList<string> errors = _codec.Validate(configuration);
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, errors));
        }

        return _codec.Serialize(configuration);
    }

    private FixedTextGeneratorConfiguration BuildConfiguration() => new()
    {
        Value = ValueTextBox.Text,
        PreserveNulls = PreserveNullsCheckBox.IsChecked == true
    };
}
