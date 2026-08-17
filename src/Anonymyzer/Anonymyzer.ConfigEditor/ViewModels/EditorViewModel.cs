namespace Anonymyzer.ConfigEditor.ViewModels;

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Anonymyzer.Configuration;

internal sealed class EditorViewModel : INotifyPropertyChanged
{
    private TableViewModel? _selectedTable;
    private string _status = "Create or open an anonymization configuration.";

    public EditorViewModel()
    {
        SemanticRoles = new[]
        {
            string.Empty,
            "Person.FirstName", "Person.LastName", "Person.FullName", "Person.BirthDate", "Person.NationalId",
            "Person.Gender",
            "Contact.Email", "Contact.Phone",
            "Address.Country", "Address.Region", "Address.City", "Address.Street", "Address.PostalCode",
            "Company.Name", "Company.TaxId",
            "Account.Login", "Financial.BankAccount"
        };

        Load(new AnonymizationConfiguration(), null);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public AnonymizationConfiguration Configuration { get; private set; } = new();
    public ObservableCollection<TableViewModel> Tables { get; } = new();
    public ObservableCollection<string> GeneratorTypes { get; } = new();
    public ObservableCollection<string> ProfileIds { get; } = new();
    public IReadOnlyList<string> SemanticRoles { get; }
    public string? CurrentPath { get; private set; }

    public TableViewModel? SelectedTable
    {
        get => _selectedTable;
        set => SetField(ref _selectedTable, value);
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

        Tables.Clear();
        foreach (TableProcessingOptions table in configuration.Tables.OrderBy(table => table.SchemaName).ThenBy(table => table.TableName))
        {
            Tables.Add(new TableViewModel(table, configuration.GeneratorProfiles));
        }

        RefreshProfiles();
        SelectedTable = Tables.FirstOrDefault();
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
            .Append("TextShuffler")
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

        Tables.Clear();
        foreach (TableProcessingOptions table in Configuration.Tables.OrderBy(table => table.SchemaName).ThenBy(table => table.TableName))
        {
            Tables.Add(new TableViewModel(table, Configuration.GeneratorProfiles));
        }

        SelectedTable = Tables.FirstOrDefault(table =>
            $"{table.Model.SchemaName}.{table.Model.TableName}".Equals(selectedTableKey, StringComparison.OrdinalIgnoreCase))
            ?? Tables.FirstOrDefault();
    }

    private static void ReplaceItems(ObservableCollection<string> target, IEnumerable<string> values)
    {
        target.Clear();
        foreach (string value in values)
        {
            target.Add(value);
        }
    }

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
