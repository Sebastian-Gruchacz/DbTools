namespace Anonymyzer.ConfigEditor.ViewModels;

using System.Collections.ObjectModel;
using Anonymyzer.Configuration;

internal sealed class TableViewModel
{
    public TableViewModel(
        TableProcessingOptions model,
        IReadOnlyList<GeneratorProfileConfiguration> profiles)
    {
        Model = model;
        Columns = new ObservableCollection<ColumnViewModel>(
            model.Columns.Select(column => new ColumnViewModel(column, profiles)));
    }

    public TableProcessingOptions Model { get; }
    public ObservableCollection<ColumnViewModel> Columns { get; }
    public int CandidateCount => Model.Columns.Count(column => column.Detection.IsCandidate);
    public string CandidateMark => CandidateCount > 0 ? "●" : string.Empty;
    public string CandidateCountText => CandidateCount > 0 ? CandidateCount.ToString() : string.Empty;
    public string CandidateDetails => CandidateCount > 0
        ? $"Automatic candidates: {CandidateCount}."
        : "No automatic candidates.";
    public string QualifiedName => $"{Model.SchemaName}.{Model.TableName}";

    public void ApplySamples(IReadOnlyDictionary<string, string> samples)
    {
        foreach (ColumnViewModel column in Columns)
        {
            column.SetSample(samples.TryGetValue(column.ColumnName, out string? sample) ? sample : "—");
        }
    }
}
