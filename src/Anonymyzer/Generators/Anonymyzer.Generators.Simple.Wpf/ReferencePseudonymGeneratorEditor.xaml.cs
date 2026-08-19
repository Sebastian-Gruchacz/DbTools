namespace Anonymyzer.Generators.Simple.Wpf;

using System.Windows;
using System.Windows.Controls;
using Anonymyzer.ConfigEditor.Abstractions;
using Newtonsoft.Json.Linq;

public partial class ReferencePseudonymGeneratorEditor : UserControl, IGeneratorConfigurationEditor
{
    private readonly ReferencePseudonymGeneratorConfigurationCodec _codec = new();

    public ReferencePseudonymGeneratorEditor(JObject options)
    {
        InitializeComponent();
        var configuration = (ReferencePseudonymGeneratorConfiguration)_codec.Deserialize(options);
        ReferenceColumnTextBox.Text = configuration.ReferenceColumn;
        LookupSchemaTextBox.Text = configuration.LookupSchema;
        LookupTableTextBox.Text = configuration.LookupTable;
        LookupKeyColumnTextBox.Text = configuration.LookupKeyColumn;
        PrefixTextBox.Text = configuration.Prefix;
        KeyEnvironmentVariableTextBox.Text = configuration.KeyEnvironmentVariable;
        HashLengthTextBox.Text = configuration.HashLength.ToString(System.Globalization.CultureInfo.InvariantCulture);
        MaximumInMemoryBytesTextBox.Text = configuration.MaximumInMemoryBytes.ToString(
            System.Globalization.CultureInfo.InvariantCulture);
        PreserveNullsCheckBox.IsChecked = configuration.PreserveNulls;
    }

    public FrameworkElement View => this;

    public IReadOnlyList<string> Validate()
    {
        return TryBuildConfiguration(out ReferencePseudonymGeneratorConfiguration configuration, out string? error)
            ? _codec.Validate(configuration)
            : [error!];
    }

    public JObject Save()
    {
        if (!TryBuildConfiguration(out ReferencePseudonymGeneratorConfiguration configuration, out string? error))
        {
            throw new InvalidOperationException(error);
        }

        IReadOnlyList<string> errors = _codec.Validate(configuration);
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, errors));
        }

        return _codec.Serialize(configuration);
    }

    private bool TryBuildConfiguration(
        out ReferencePseudonymGeneratorConfiguration configuration,
        out string? error)
    {
        configuration = new ReferencePseudonymGeneratorConfiguration();
        if (!int.TryParse(HashLengthTextBox.Text, out int hashLength))
        {
            error = "HashLength must be an integer.";
            return false;
        }

        if (!long.TryParse(MaximumInMemoryBytesTextBox.Text, out long maximumInMemoryBytes))
        {
            error = "MaximumInMemoryBytes must be an integer.";
            return false;
        }

        configuration.ReferenceColumn = ReferenceColumnTextBox.Text.Trim();
        configuration.LookupSchema = LookupSchemaTextBox.Text.Trim();
        configuration.LookupTable = LookupTableTextBox.Text.Trim();
        configuration.LookupKeyColumn = LookupKeyColumnTextBox.Text.Trim();
        configuration.Prefix = PrefixTextBox.Text;
        configuration.KeyEnvironmentVariable = KeyEnvironmentVariableTextBox.Text.Trim();
        configuration.HashLength = hashLength;
        configuration.MaximumInMemoryBytes = maximumInMemoryBytes;
        configuration.PreserveNulls = PreserveNullsCheckBox.IsChecked == true;
        error = null;
        return true;
    }
}
