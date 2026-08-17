namespace Anonymyzer.ConfigEditor.ViewModels;

using System.Collections.ObjectModel;
using Anonymyzer.Configuration;

internal sealed class TableViewModel
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

    public TableProcessingOptions Model { get; }
    public ObservableCollection<ColumnViewModel> Columns { get; } = new();
    public ObservableCollection<ColumnViewModel> HiddenColumns { get; } = new();
    public int CandidateCount => Model.Columns.Count(column => column.Detection.IsCandidate);
    public string CandidateMark => CandidateCount > 0 ? "●" : string.Empty;
    public string CandidateCountText => CandidateCount > 0 ? CandidateCount.ToString() : string.Empty;
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
    }

    public void ApplySamples(IReadOnlyDictionary<string, string> samples)
    {
        foreach (ColumnViewModel column in Columns)
        {
            column.SetSample(samples.TryGetValue(column.ColumnName, out string? sample) ? sample : "—");
        }
    }

    private ColumnViewModel CreateColumnViewModel(ColumnProcessingOptions column) =>
        new(column, _profiles, _semanticRoleGroups);

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
        column.Detection.IsCandidate
        || column.Enabled
        || !string.IsNullOrWhiteSpace(column.SemanticRole)
        || !string.IsNullOrWhiteSpace(column.GenerationGroupId);
}
