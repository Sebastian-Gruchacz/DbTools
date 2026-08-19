namespace Anonymyzer.ConfigEditor;

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using Anonymyzer.Configuration;
using Anonymyzer.DatabaseAccess;

public partial class ColumnSamplesWindow : Window, INotifyPropertyChanged
{
    private readonly AnonymizationConfiguration _configuration;
    private readonly TableProcessingOptions _table;
    private readonly ColumnProcessingOptions _column;
    private readonly ColumnSampleReader _reader = new();
    private readonly JsonSampleProfiler _jsonProfiler = new();
    private string _status = "Ready to load non-null values.";
    private string _jsonProfileSummary = "JSON profile: load samples to analyze.";

    public ColumnSamplesWindow(
        AnonymizationConfiguration configuration,
        TableProcessingOptions table,
        ColumnProcessingOptions column)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _table = table ?? throw new ArgumentNullException(nameof(table));
        _column = column ?? throw new ArgumentNullException(nameof(column));
        InitializeComponent();
        DataContext = this;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string TargetName => $"{_table.SchemaName}.{_table.TableName}.{_column.ColumnName}";

    public ObservableCollection<ColumnSample> Samples { get; } = new();

    public ObservableCollection<JsonPathProfile> JsonPaths { get; } = new();

    public string JsonProfileSummary
    {
        get => _jsonProfileSummary;
        private set
        {
            if (_jsonProfileSummary == value)
            {
                return;
            }

            _jsonProfileSummary = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(JsonProfileSummary)));
        }
    }

    public string Status
    {
        get => _status;
        private set
        {
            if (_status == value)
            {
                return;
            }

            _status = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Status)));
        }
    }

    private async void Load_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (!int.TryParse(MaximumRowsTextBox.Text, out int maximumRows) || maximumRows is < 1 or > 50)
            {
                throw new InvalidOperationException("Rows must be a number between 1 and 50.");
            }

            LoadButton.IsEnabled = false;
            Status = "Validating clone and reading values...";
            IReadOnlyList<ColumnSample> samples = await _reader.ReadAsync(
                _configuration,
                _table,
                _column,
                ConnectionEnvironmentTextBox.Text.Trim(),
                maximumRows);

            Samples.Clear();
            foreach (ColumnSample sample in samples)
            {
                Samples.Add(sample);
            }

            JsonSampleProfile jsonProfile = _jsonProfiler.Profile(samples);
            JsonPaths.Clear();
            foreach (JsonPathProfile path in jsonProfile.Paths)
            {
                JsonPaths.Add(path);
            }

            JsonProfileSummary = jsonProfile.Summary;

            Status = samples.Count == 0
                ? "No non-null values found."
                : $"Loaded {samples.Count} read-only value(s).";
        }
        catch (Exception exception)
        {
            Status = exception.Message;
            MessageBox.Show(this, exception.Message, "Sample read error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            LoadButton.IsEnabled = true;
        }
    }

    private void CopyAll_Click(object sender, RoutedEventArgs e)
    {
        if (Samples.Count == 0)
        {
            return;
        }

        try
        {
            Clipboard.SetText(string.Join(Environment.NewLine, Samples.Select(sample => sample.Value)));
            Status = $"Copied {Samples.Count} value(s) to the clipboard.";
        }
        catch (Exception exception)
        {
            Status = exception.Message;
            MessageBox.Show(this, exception.Message, "Clipboard error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
