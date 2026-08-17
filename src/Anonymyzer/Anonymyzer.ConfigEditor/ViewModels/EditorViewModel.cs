namespace Anonymyzer.ConfigEditor.ViewModels;

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Anonymyzer.Configuration;

internal sealed class EditorViewModel : INotifyPropertyChanged
{
    private readonly List<TableViewModel> _allTables = new();
    private TableViewModel? _selectedTable;
    private string _tableFilterText = string.Empty;
    private bool _showCandidateTablesOnly;
    private string _status = "Create or open an anonymization configuration.";

    public EditorViewModel()
    {
        SemanticRoleGroups = SemanticRoleCatalog.CreateDefault();

        Load(new AnonymizationConfiguration(), null);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public AnonymizationConfiguration Configuration { get; private set; } = new();
    public ObservableCollection<TableViewModel> Tables { get; } = new();
    public ObservableCollection<string> GeneratorTypes { get; } = new();
    public ObservableCollection<string> ProfileIds { get; } = new();
    public IReadOnlyList<SemanticRoleGroup> SemanticRoleGroups { get; }
    public string? CurrentPath { get; private set; }
    public string CandidateTablesOnlyLabel =>
        $"Only candidates ({_allTables.Count(table => table.CandidateCount > 0)})";
    public string TableFilterSummary => $"{Tables.Count} / {_allTables.Count} tables";

    public TableViewModel? SelectedTable
    {
        get => _selectedTable;
        set => SetField(ref _selectedTable, value);
    }

    public string TableFilterText
    {
        get => _tableFilterText;
        set
        {
            if (SetField(ref _tableFilterText, value ?? string.Empty))
            {
                ApplyTableFilter();
            }
        }
    }

    public bool ShowCandidateTablesOnly
    {
        get => _showCandidateTablesOnly;
        set
        {
            if (SetField(ref _showCandidateTablesOnly, value))
            {
                ApplyTableFilter();
            }
        }
    }

    public string Status
    {
        get => _status;
        set => SetField(ref _status, value);
    }

    public void Load(AnonymizationConfiguration configuration, string? path)
    {
        Configuration = configuration;
        CurrentPath = path;
        _tableFilterText = string.Empty;
        _showCandidateTablesOnly = false;
        OnPropertyChanged(nameof(TableFilterText));
        OnPropertyChanged(nameof(ShowCandidateTablesOnly));

        RebuildAllTables();
        RefreshProfiles();
        ApplyTableFilter();
        Status = path is null ? "New configuration." : $"Opened {path}";
    }

    public void SetCurrentPath(string path)
    {
        CurrentPath = path;
        Status = $"Saved {path}";
    }

    public void RefreshProfiles()
    {
        ReplaceItems(GeneratorTypes, Configuration.GeneratorProfiles
            .Select(profile => profile.GeneratorType)
            .Concat(["EmailAddress", "FixedText", "SequentialText", "TextShuffler"])
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value));

        ReplaceItems(ProfileIds, Configuration.GeneratorProfiles
            .Select(profile => profile.Id)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value));
    }

    public void RefreshTables()
    {
        string? selectedTableKey = SelectedTable is null
            ? null
            : $"{SelectedTable.Model.SchemaName}.{SelectedTable.Model.TableName}";

        RebuildAllTables(preserveVisibleColumns: true);
        ApplyTableFilter(selectedTableKey);
    }

    private void RebuildAllTables(bool preserveVisibleColumns = false)
    {
        Dictionary<string, IReadOnlySet<string>> visibleColumns = preserveVisibleColumns
            ? _allTables.ToDictionary(
                table => table.QualifiedName,
                table => (IReadOnlySet<string>)table.Columns
                    .Select(column => column.ColumnName)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase),
                StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, IReadOnlySet<string>>(StringComparer.OrdinalIgnoreCase);

        _allTables.Clear();
        _allTables.AddRange(Configuration.Tables
            .OrderBy(table => table.SchemaName)
            .ThenBy(table => table.TableName)
            .Select(table =>
            {
                string key = $"{table.SchemaName}.{table.TableName}";
                visibleColumns.TryGetValue(key, out IReadOnlySet<string>? previouslyVisible);
                return new TableViewModel(
                    table,
                    Configuration.GeneratorProfiles,
                    SemanticRoleGroups,
                    previouslyVisible);
            }));
    }

    private void ApplyTableFilter(string? preferredTableKey = null)
    {
        preferredTableKey ??= SelectedTable?.QualifiedName;
        string[] terms = _tableFilterText.Split(
            ' ',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        IEnumerable<TableViewModel> visibleTables = _allTables
            .Where(table => !_showCandidateTablesOnly || table.CandidateCount > 0)
            .Where(table => terms.All(term =>
                table.QualifiedName.Contains(term, StringComparison.OrdinalIgnoreCase)));

        Tables.Clear();
        foreach (TableViewModel table in visibleTables)
        {
            Tables.Add(table);
        }

        SelectedTable = Tables.FirstOrDefault(table =>
            table.QualifiedName.Equals(preferredTableKey, StringComparison.OrdinalIgnoreCase))
            ?? Tables.FirstOrDefault();
        OnPropertyChanged(nameof(CandidateTablesOnlyLabel));
        OnPropertyChanged(nameof(TableFilterSummary));
    }

    private static void ReplaceItems(ObservableCollection<string> target, IEnumerable<string> values)
    {
        target.Clear();
        foreach (string value in values)
        {
            target.Add(value);
        }
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
