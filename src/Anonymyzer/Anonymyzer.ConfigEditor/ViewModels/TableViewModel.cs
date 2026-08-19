namespace Anonymyzer.ConfigEditor.ViewModels;

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Anonymyzer.Configuration;

internal sealed class TableViewModel : INotifyPropertyChanged
{
    private readonly IReadOnlyList<GeneratorProfileConfiguration> _profiles;
    private readonly IReadOnlyList<SemanticRoleGroup> _semanticRoleGroups;

    public TableViewModel(
        TableProcessingOptions model,
        IReadOnlyList<GeneratorProfileConfiguration> profiles,
        IReadOnlyList<SemanticRoleGroup> semanticRoleGroups,
        IReadOnlySet<string>? previouslyVisibleColumns = null)
    {
        Model = model;
        _profiles = profiles;
        _semanticRoleGroups = semanticRoleGroups;

        foreach (ColumnProcessingOptions column in model.Columns.OrderBy(column => column.Ordinal))
        {
            ColumnViewModel viewModel = CreateColumnViewModel(column);
            if (ShouldShowInitially(column) || previouslyVisibleColumns?.Contains(column.ColumnName) == true)
            {
                Columns.Add(viewModel);
            }
            else
            {
                HiddenColumns.Add(viewModel);
            }
        }
    }

    public event EventHandler? ConfigurationChanged;
    public event PropertyChangedEventHandler? PropertyChanged;

    public TableProcessingOptions Model { get; }
    public ObservableCollection<ColumnViewModel> Columns { get; } = new();
    public ObservableCollection<ColumnViewModel> HiddenColumns { get; } = new();
    public int CandidateCount => Model.Columns.Count(column => column.Detection.IsCandidate);
    public string CandidateMark => CandidateCount > 0 ? "●" : string.Empty;
    public string CandidateCountText => CandidateCount > 0 ? CandidateCount.ToString() : string.Empty;
    public int ManualOverrideCount => Model.Columns.Count(column => column.OperatorOverrides?.HasAny == true);
    public string ManualMark => ManualOverrideCount > 0 ? "◆" : string.Empty;
    public string ManualDetails => ManualOverrideCount > 0
        ? $"Columns with operator choices: {ManualOverrideCount}."
        : "No explicit operator choices.";
    public int MissingColumnCount => Model.Columns.Count(column =>
        string.Equals(column.SchemaStatus, "Missing", StringComparison.OrdinalIgnoreCase));
    public string SchemaWarningMark => string.Equals(Model.SchemaStatus, "Missing", StringComparison.OrdinalIgnoreCase)
                                       || MissingColumnCount > 0
        ? "⚠"
        : string.Empty;
    public string SchemaWarningDetails => string.Equals(Model.SchemaStatus, "Missing", StringComparison.OrdinalIgnoreCase)
        ? "Table was not found during the latest database rescan; its saved configuration was retained."
        : MissingColumnCount > 0
            ? $"Columns retained but missing from the latest database rescan: {MissingColumnCount}."
            : "Table and configured columns are present in the latest database scan.";
    public string CandidateDetails => CandidateCount > 0
        ? $"Automatic candidates: {CandidateCount}."
        : "No automatic candidates.";
    public string QualifiedName => $"{Model.SchemaName}.{Model.TableName}";

    public void RevealColumn(ColumnViewModel column)
    {
        if (!HiddenColumns.Remove(column))
        {
            return;
        }

        InsertVisible(column);
    }

    public void AddColumn(ColumnProcessingOptions column)
    {
        Model.Columns.Add(column);
        Model.Columns = Model.Columns.OrderBy(candidate => candidate.Ordinal).ToList();
        InsertVisible(CreateColumnViewModel(column));
        OnConfigurationChanged();
    }

    public void ApplySamples(IReadOnlyDictionary<string, string> samples)
    {
        foreach (ColumnViewModel column in Columns)
        {
            column.SetSample(samples.TryGetValue(column.ColumnName, out string? sample) ? sample : "—");
        }
    }

    private ColumnViewModel CreateColumnViewModel(ColumnProcessingOptions column)
    {
        var viewModel = new ColumnViewModel(column, _profiles, _semanticRoleGroups);
        viewModel.ConfigurationChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(ManualOverrideCount));
            OnPropertyChanged(nameof(ManualMark));
            OnPropertyChanged(nameof(ManualDetails));
            OnConfigurationChanged();
        };
        return viewModel;
    }

    private void InsertVisible(ColumnViewModel column)
    {
        int index = 0;
        while (index < Columns.Count && Columns[index].Ordinal < column.Ordinal)
        {
            index++;
        }

        Columns.Insert(index, column);
    }

    private static bool ShouldShowInitially(ColumnProcessingOptions column) =>
        string.Equals(column.SchemaStatus, "Missing", StringComparison.OrdinalIgnoreCase)
        || column.Detection.IsCandidate
        || column.Enabled
        || !string.IsNullOrWhiteSpace(column.SemanticRole)
        || !string.IsNullOrWhiteSpace(column.GenerationGroupId);

    private void OnConfigurationChanged()
    {
        ConfigurationChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
