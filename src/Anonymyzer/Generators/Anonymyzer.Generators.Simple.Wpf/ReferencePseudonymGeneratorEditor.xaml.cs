namespace Anonymyzer.Generators.Simple.Wpf;

using System.Windows;
using System.Windows.Controls;
using Anonymyzer.ConfigEditor.Abstractions;
using Newtonsoft.Json.Linq;

public partial class ReferencePseudonymGeneratorEditor : UserControl, IGeneratorConfigurationEditor
{
    private readonly ReferencePseudonymGeneratorConfigurationCodec _codec = new();
    private readonly GeneratorConfigurationEditorContext _context;
    private bool _isLoading = true;

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
        ReferenceColumnComboBox.Text = configuration.ReferenceColumn;
        RefreshLookupSchemas(configuration.LookupSchema);
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
        _isLoading = false;
    }

    public FrameworkElement View => this;

    private void ReferenceColumn_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoading)
        {
            return;
        }

        string selectedSchema = CurrentText(LookupSchemaComboBox);
        string selectedTable = CurrentText(LookupTableComboBox);
        string selectedColumn = CurrentText(LookupKeyColumnComboBox);
        GeneratorConfigurationForeignKeyOption[] matches = MatchingForeignKeys()
            .Where(foreignKey => foreignKey.ReferencedColumns.Count == 1)
            .GroupBy(
                foreignKey => $"{foreignKey.ReferencedSchemaName}\u001f" +
                              $"{foreignKey.ReferencedTableName}\u001f{foreignKey.ReferencedColumns[0]}",
                StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToArray();
        if (matches.Length == 1)
        {
            selectedSchema = matches[0].ReferencedSchemaName;
            selectedTable = matches[0].ReferencedTableName;
            selectedColumn = matches[0].ReferencedColumns[0];
        }

        RefreshLookupSchemas(selectedSchema);
        RefreshLookupTables(selectedTable);
        RefreshLookupColumns(selectedColumn);
    }

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
        string[] relatedTables = MatchingForeignKeys()
            .Where(foreignKey => foreignKey.ReferencedSchemaName.Equals(schema, StringComparison.OrdinalIgnoreCase))
            .Select(foreignKey => foreignKey.ReferencedTableName)
            .ToArray();
        LookupTableComboBox.ItemsSource = Prioritize(relatedTables, _context.Tables
            .Where(table => table.SchemaName.Equals(schema, StringComparison.OrdinalIgnoreCase))
            .Select(table => table.TableName));
        LookupTableComboBox.Text = selectedTable;
    }

    private void RefreshLookupColumns(string selectedColumn)
    {
        string schema = CurrentText(LookupSchemaComboBox);
        string tableName = CurrentText(LookupTableComboBox);
        string[] relatedColumns = MatchingForeignKeys()
            .Where(foreignKey => foreignKey.ReferencedSchemaName.Equals(schema, StringComparison.OrdinalIgnoreCase)
                                 && foreignKey.ReferencedTableName.Equals(tableName, StringComparison.OrdinalIgnoreCase)
                                 && foreignKey.Columns.Count == 1
                                 && foreignKey.ReferencedColumns.Count == 1)
            .SelectMany(foreignKey => foreignKey.ReferencedColumns)
            .ToArray();
        LookupKeyColumnComboBox.ItemsSource = Prioritize(relatedColumns, _context.Tables
            .Where(table => table.SchemaName.Equals(schema, StringComparison.OrdinalIgnoreCase)
                            && table.TableName.Equals(tableName, StringComparison.OrdinalIgnoreCase))
            .SelectMany(table => table.PrimaryKeyColumns
                .OrderBy(column => column, StringComparer.OrdinalIgnoreCase)
                .Concat(table.Columns
                    .Where(column => !table.PrimaryKeyColumns.Contains(column, StringComparer.OrdinalIgnoreCase))
                    .OrderBy(column => column, StringComparer.OrdinalIgnoreCase))));
        LookupKeyColumnComboBox.Text = selectedColumn;
    }

    private void RefreshLookupSchemas(string selectedSchema)
    {
        LookupSchemaComboBox.ItemsSource = Prioritize(
            MatchingForeignKeys().Select(foreignKey => foreignKey.ReferencedSchemaName),
            _context.Tables.Select(table => table.SchemaName));
        LookupSchemaComboBox.Text = selectedSchema;
    }

    private IEnumerable<GeneratorConfigurationForeignKeyOption> MatchingForeignKeys()
    {
        string referenceColumn = CurrentText(ReferenceColumnComboBox);
        return _context.Tables
            .SelectMany(table => table.ForeignKeys)
            .Where(foreignKey => foreignKey.Columns.Count == 1
                                 && foreignKey.Columns[0].Equals(
                                     referenceColumn,
                                     StringComparison.OrdinalIgnoreCase));
    }

    private static string[] Prioritize(IEnumerable<string> preferred, IEnumerable<string> all) => preferred
        .Concat(all.Order(StringComparer.OrdinalIgnoreCase))
        .Where(value => !string.IsNullOrWhiteSpace(value))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();

    private static string CurrentText(ComboBox comboBox) =>
        (comboBox.SelectedItem as string ?? comboBox.Text).Trim();
}
