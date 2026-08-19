namespace Anonymyzer.Generators.Simple.Wpf;

using System.Windows;
using System.Windows.Controls;
using Anonymyzer.ConfigEditor.Abstractions;
using Newtonsoft.Json.Linq;

public partial class ReferencePseudonymGeneratorEditor : UserControl, IGeneratorConfigurationEditor
{
    private readonly ReferencePseudonymGeneratorConfigurationCodec _codec = new();
    private readonly GeneratorConfigurationEditorContext _context;

    public ReferencePseudonymGeneratorEditor(
        JObject options,
        GeneratorConfigurationEditorContext? context = null)
    {
        InitializeComponent();
        _context = context ?? GeneratorConfigurationEditorContext.Empty;
        var configuration = (ReferencePseudonymGeneratorConfiguration)_codec.Deserialize(options);
        ReferenceColumnComboBox.ItemsSource = _context.Tables
            .SelectMany(table => table.Columns)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        LookupSchemaComboBox.ItemsSource = _context.Tables
            .Select(table => table.SchemaName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        ReferenceColumnComboBox.Text = configuration.ReferenceColumn;
        LookupSchemaComboBox.Text = configuration.LookupSchema;
        RefreshLookupTables(configuration.LookupTable);
        RefreshLookupColumns(configuration.LookupKeyColumn);
        PrefixTextBox.Text = configuration.Prefix;
        KeyEnvironmentVariableTextBox.Text = configuration.KeyEnvironmentVariable;
        HashLengthTextBox.Text = configuration.HashLength.ToString(System.Globalization.CultureInfo.InvariantCulture);
        MaximumInMemoryBytesTextBox.Text = configuration.MaximumInMemoryBytes.ToString(
            System.Globalization.CultureInfo.InvariantCulture);
        OverflowStrategyComboBox.ItemsSource = Enum.GetValues<RelationalLookupOverflowStrategy>();
        OverflowStrategyComboBox.SelectedItem = configuration.OverflowStrategy;
        PreserveNullsCheckBox.IsChecked = configuration.PreserveNulls;
    }

    public FrameworkElement View => this;

    private void LookupSchema_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        RefreshLookupTables(string.Empty);

    private void LookupTable_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        RefreshLookupColumns(string.Empty);

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

        configuration.ReferenceColumn = CurrentText(ReferenceColumnComboBox);
        configuration.LookupSchema = CurrentText(LookupSchemaComboBox);
        configuration.LookupTable = CurrentText(LookupTableComboBox);
        configuration.LookupKeyColumn = CurrentText(LookupKeyColumnComboBox);
        configuration.Prefix = PrefixTextBox.Text;
        configuration.KeyEnvironmentVariable = KeyEnvironmentVariableTextBox.Text.Trim();
        configuration.HashLength = hashLength;
        configuration.MaximumInMemoryBytes = maximumInMemoryBytes;
        configuration.OverflowStrategy = OverflowStrategyComboBox.SelectedItem is RelationalLookupOverflowStrategy strategy
            ? strategy
            : RelationalLookupOverflowStrategy.Fail;
        configuration.PreserveNulls = PreserveNullsCheckBox.IsChecked == true;
        error = null;
        return true;
    }

    private void RefreshLookupTables(string selectedTable)
    {
        string schema = CurrentText(LookupSchemaComboBox);
        LookupTableComboBox.ItemsSource = _context.Tables
            .Where(table => table.SchemaName.Equals(schema, StringComparison.OrdinalIgnoreCase))
            .Select(table => table.TableName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        LookupTableComboBox.Text = selectedTable;
    }

    private void RefreshLookupColumns(string selectedColumn)
    {
        string schema = CurrentText(LookupSchemaComboBox);
        string tableName = CurrentText(LookupTableComboBox);
        LookupKeyColumnComboBox.ItemsSource = _context.Tables
            .Where(table => table.SchemaName.Equals(schema, StringComparison.OrdinalIgnoreCase)
                            && table.TableName.Equals(tableName, StringComparison.OrdinalIgnoreCase))
            .SelectMany(table => table.PrimaryKeyColumns
                .OrderBy(column => column, StringComparer.OrdinalIgnoreCase)
                .Concat(table.Columns
                    .Where(column => !table.PrimaryKeyColumns.Contains(column, StringComparer.OrdinalIgnoreCase))
                    .OrderBy(column => column, StringComparer.OrdinalIgnoreCase)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        LookupKeyColumnComboBox.Text = selectedColumn;
    }

    private static string CurrentText(ComboBox comboBox) =>
        (comboBox.SelectedItem as string ?? comboBox.Text).Trim();
}
