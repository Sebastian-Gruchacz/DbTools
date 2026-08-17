namespace Anonymyzer.ConfigEditor;

using System.Collections.ObjectModel;
using System.Windows;
using Anonymyzer.Configuration;
using Anonymyzer.DatabaseAccess;

public partial class AddColumnsWindow : Window
{
    private readonly AnonymizationConfiguration _configuration;
    private readonly TableProcessingOptions _table;
    private readonly ColumnMetadataReader _reader = new();

    public AddColumnsWindow(
        AnonymizationConfiguration configuration,
        TableProcessingOptions table)
    {
        _configuration = configuration;
        _table = table;
        Target = $"{table.SchemaName}.{table.TableName}";
        InitializeComponent();
        DataContext = this;
    }

    public string Target { get; }
    public ObservableCollection<AvailableColumnViewModel> Columns { get; } = new();
    public IReadOnlyList<AvailableColumn> SelectedColumns { get; private set; } = Array.Empty<AvailableColumn>();

    private async void Load_Click(object sender, RoutedEventArgs e)
    {
        string connectionEnvironmentVariable = ConnectionEnvironmentTextBox.Text.Trim();
        if (connectionEnvironmentVariable.Length == 0)
        {
            MessageBox.Show(this, "Enter the name of the connection-string environment variable.",
                "Missing connection", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            LoadButton.IsEnabled = false;
            StatusTextBlock.Text = "Validating clone and loading metadata...";
            IReadOnlyList<AvailableColumn> columns = await _reader.ReadAvailableAsync(
                _configuration,
                _table,
                connectionEnvironmentVariable);
            Columns.Clear();
            foreach (AvailableColumn column in columns)
            {
                Columns.Add(new AvailableColumnViewModel(column));
            }

            StatusTextBlock.Text = columns.Count == 0
                ? "No unconfigured columns are available."
                : $"Loaded {columns.Count} unconfigured column(s).";
        }
        catch (Exception exception)
        {
            StatusTextBlock.Text = exception.Message;
            MessageBox.Show(this, exception.Message, "Metadata error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            LoadButton.IsEnabled = true;
        }
    }

    private void AddSelected_Click(object sender, RoutedEventArgs e)
    {
        SelectedColumns = Columns
            .Where(column => column.IsSelected)
            .Select(column => column.Model)
            .ToArray();
        if (SelectedColumns.Count == 0)
        {
            MessageBox.Show(this, "Select at least one column.",
                "No columns selected", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        DialogResult = true;
    }
}

public sealed class AvailableColumnViewModel(AvailableColumn model)
{
    public AvailableColumn Model { get; } = model;
    public bool IsSelected { get; set; }
    public int Ordinal => Model.Ordinal;
    public string ColumnName => Model.ColumnName;
    public string TypeDisplay => Model.DataType.Equals("Text", StringComparison.OrdinalIgnoreCase)
        ? $"{Model.DataType} ({(Model.MaxLength <= 0 ? "MAX" : Model.MaxLength)})"
        : Model.DataType;
}
