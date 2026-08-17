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
    public string DisplayName => $"{(Model.HasCandidates ? "● " : string.Empty)}{Model.SchemaName}.{Model.TableName}";

    public void ApplySamples(IReadOnlyDictionary<string, string> samples)
    {
        foreach (ColumnViewModel column in Columns)
        {
            column.SetSample(samples.TryGetValue(column.ColumnName, out string? sample) ? sample : "—");
        }
    }
}
