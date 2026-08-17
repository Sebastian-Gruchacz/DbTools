namespace Anonymyzer.ConfigEditor;

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using Anonymyzer.Base.Generation;
using Anonymyzer.Configuration;

public partial class GenerationGroupsWindow : Window, INotifyPropertyChanged
{
    private readonly TableProcessingOptions _table;
    private readonly IReadOnlyList<GeneratorProfileConfiguration> _profiles;
    private readonly IReadOnlyList<GeneratorDescriptor> _descriptors;
    private GroupRow? _selectedGroup;

    public GenerationGroupsWindow(
        TableProcessingOptions table,
        IReadOnlyList<GeneratorProfileConfiguration> profiles,
        IReadOnlyList<GeneratorDescriptor> descriptors)
    {
        InitializeComponent();
        _table = table;
        _profiles = profiles;
        _descriptors = descriptors;
        Groups = new ObservableCollection<GroupRow>(table.GenerationGroups.Select(GroupRow.FromModel));
        ProfileIds = profiles.Select(profile => profile.Id).OrderBy(value => value).ToArray();
        OutputNames = descriptors.SelectMany(descriptor => descriptor.Outputs).Select(output => output.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(value => value).ToArray();
        ColumnNames = table.Columns.Select(column => column.ColumnName).ToArray();
        DataContext = this;
        SelectedGroup = Groups.FirstOrDefault();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<GroupRow> Groups { get; }
    public IReadOnlyList<string> ProfileIds { get; }
    public IReadOnlyList<string> OutputNames { get; }
    public IReadOnlyList<string> ColumnNames { get; }

    public GroupRow? SelectedGroup
    {
        get => _selectedGroup;
        set
        {
            if (_selectedGroup == value)
            {
                return;
            }

            _selectedGroup = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedGroup)));
            BindingsGrid.ItemsSource = value?.Bindings;
        }
    }

    private void GroupsGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        SelectedGroup = GroupsGrid.SelectedItem as GroupRow;
    }

    private void AddGroup_Click(object sender, RoutedEventArgs e)
    {
        GeneratorProfileConfiguration? profile = _profiles.FirstOrDefault(item =>
            item.GeneratorType.Equals("PersonIdentity", StringComparison.OrdinalIgnoreCase))
            ?? _profiles.FirstOrDefault();
        var row = new GroupRow
        {
            Id = CreateUniqueGroupId(),
            ProfileId = profile?.Id ?? string.Empty,
            Locale = profile?.Locale ?? string.Empty
        };
        Groups.Add(row);
        SelectedGroup = row;
        GroupsGrid.SelectedItem = row;
        AddMissingBindings(row);
    }

    private void RemoveGroup_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedGroup is not null)
        {
            Groups.Remove(SelectedGroup);
            SelectedGroup = Groups.FirstOrDefault();
        }
    }

    private void AddBinding_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedGroup is null)
        {
            return;
        }

        string output = GetDescriptor(SelectedGroup.ProfileId)?.Outputs
            .Select(item => item.Name)
            .FirstOrDefault(name => SelectedGroup.Bindings.All(binding =>
                !binding.Output.Equals(name, StringComparison.OrdinalIgnoreCase)))
            ?? string.Empty;
        string columnName = ColumnNames.FirstOrDefault(name => Groups.SelectMany(group => group.Bindings).All(binding =>
            !binding.ColumnName.Equals(name, StringComparison.OrdinalIgnoreCase))) ?? string.Empty;
        var bindingRow = new BindingRow { Output = output, ColumnName = columnName };
        SelectedGroup.Bindings.Add(bindingRow);
        BindingsGrid.SelectedItem = bindingRow;
    }

    private void RemoveBinding_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedGroup is not null && BindingsGrid.SelectedItem is BindingRow binding)
        {
            SelectedGroup.Bindings.Remove(binding);
        }
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        GroupsGrid.CommitEdit(DataGridEditingUnit.Cell, exitEditingMode: true);
        GroupsGrid.CommitEdit(DataGridEditingUnit.Row, exitEditingMode: true);
        BindingsGrid.CommitEdit(DataGridEditingUnit.Cell, exitEditingMode: true);
        BindingsGrid.CommitEdit(DataGridEditingUnit.Row, exitEditingMode: true);

        try
        {
            List<GenerationGroupConfiguration> groups = BuildValidatedGroups();
            ApplyGroups(groups);
            DialogResult = true;
        }
        catch (InvalidOperationException exception)
        {
            MessageBox.Show(this, exception.Message, "Invalid generation groups", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private List<GenerationGroupConfiguration> BuildValidatedGroups()
    {
        var result = new List<GenerationGroupConfiguration>();
        var groupIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var boundColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (GroupRow row in Groups)
        {
            if (string.IsNullOrWhiteSpace(row.Id) || !groupIds.Add(row.Id))
            {
                throw new InvalidOperationException($"Group id '{row.Id}' is empty or duplicated.");
            }

            GeneratorProfileConfiguration profile = _profiles.FirstOrDefault(item =>
                item.Id.Equals(row.ProfileId, StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidOperationException($"Group '{row.Id}' references missing profile '{row.ProfileId}'.");
            GeneratorDescriptor descriptor = GetDescriptor(profile)
                ?? throw new InvalidOperationException($"Generator {profile.GeneratorType} {profile.GeneratorVersion} is not installed.");
            var bindings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (BindingRow binding in row.Bindings)
            {
                if (!descriptor.Outputs.Any(output => output.Name.Equals(binding.Output, StringComparison.OrdinalIgnoreCase)))
                {
                    throw new InvalidOperationException($"Group '{row.Id}' contains unknown output '{binding.Output}'.");
                }

                if (!ColumnNames.Contains(binding.ColumnName, StringComparer.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException($"Group '{row.Id}' references missing column '{binding.ColumnName}'.");
                }

                if (!bindings.TryAdd(binding.Output, binding.ColumnName))
                {
                    throw new InvalidOperationException($"Output '{binding.Output}' is duplicated in group '{row.Id}'.");
                }

                if (!boundColumns.Add(binding.ColumnName))
                {
                    throw new InvalidOperationException($"Column '{binding.ColumnName}' is bound more than once.");
                }
            }

            foreach (GeneratorOutputDescriptor requiredOutput in descriptor.Outputs.Where(output => output.Required))
            {
                if (!bindings.ContainsKey(requiredOutput.Name))
                {
                    throw new InvalidOperationException($"Group '{row.Id}' must bind output '{requiredOutput.Name}'.");
                }
            }

            if (bindings.Count == 0)
            {
                throw new InvalidOperationException($"Group '{row.Id}' has no bindings.");
            }

            result.Add(new GenerationGroupConfiguration
            {
                Id = row.Id.Trim(),
                GeneratorType = profile.GeneratorType,
                GeneratorVersion = profile.GeneratorVersion,
                ProfileId = profile.Id,
                Locale = string.IsNullOrWhiteSpace(row.Locale) ? profile.Locale : row.Locale.Trim(),
                Bindings = bindings
            });
        }

        return result;
    }

    private void ApplyGroups(List<GenerationGroupConfiguration> groups)
    {
        _table.GenerationGroups.Clear();
        _table.GenerationGroups.AddRange(groups);
        foreach (ColumnProcessingOptions column in _table.Columns)
        {
            column.GenerationGroupId = string.Empty;
        }

        foreach (GenerationGroupConfiguration group in groups)
        {
            GeneratorDescriptor descriptor = GetDescriptor(group.GeneratorType, group.GeneratorVersion)!;
            foreach ((string outputName, string columnName) in group.Bindings)
            {
                ColumnProcessingOptions column = _table.Columns.Single(item =>
                    item.ColumnName.Equals(columnName, StringComparison.OrdinalIgnoreCase));
                column.GenerationGroupId = group.Id;
                column.Generator = new ColumnGeneratorConfiguration();

                GeneratorOutputDescriptor? output = descriptor.Outputs.FirstOrDefault(item =>
                    item.Name.Equals(outputName, StringComparison.OrdinalIgnoreCase));
                if (string.IsNullOrWhiteSpace(column.SemanticRole) && output is not null)
                {
                    column.SemanticRole = output.SemanticRole;
                }
            }
        }
    }

    private void AddMissingBindings(GroupRow row)
    {
        GeneratorDescriptor? descriptor = GetDescriptor(row.ProfileId);
        if (descriptor is null)
        {
            return;
        }

        foreach (GeneratorOutputDescriptor output in descriptor.Outputs)
        {
            string? matchingColumn = _table.Columns.FirstOrDefault(column =>
                column.SemanticRole.Equals(output.SemanticRole, StringComparison.OrdinalIgnoreCase)
                && Groups.SelectMany(group => group.Bindings).All(binding =>
                    !binding.ColumnName.Equals(column.ColumnName, StringComparison.OrdinalIgnoreCase)))?.ColumnName;
            if (matchingColumn is not null)
            {
                row.Bindings.Add(new BindingRow { Output = output.Name, ColumnName = matchingColumn });
            }
        }
    }

    private GeneratorDescriptor? GetDescriptor(string profileId)
    {
        GeneratorProfileConfiguration? profile = _profiles.FirstOrDefault(item =>
            item.Id.Equals(profileId, StringComparison.OrdinalIgnoreCase));
        return profile is null ? null : GetDescriptor(profile);
    }

    private GeneratorDescriptor? GetDescriptor(GeneratorProfileConfiguration profile)
    {
        return GetDescriptor(profile.GeneratorType, profile.GeneratorVersion);
    }

    private GeneratorDescriptor? GetDescriptor(string generatorType, string generatorVersion)
    {
        return _descriptors.FirstOrDefault(descriptor =>
            descriptor.Type.Equals(generatorType, StringComparison.OrdinalIgnoreCase)
            && descriptor.Version.Equals(generatorVersion, StringComparison.Ordinal));
    }

    private string CreateUniqueGroupId()
    {
        for (int index = 1; ; index++)
        {
            string candidate = $"group-{index}";
            if (Groups.All(group => !group.Id.Equals(candidate, StringComparison.OrdinalIgnoreCase)))
            {
                return candidate;
            }
        }
    }

    public sealed class GroupRow
    {
        public string Id { get; set; } = string.Empty;
        public string ProfileId { get; set; } = string.Empty;
        public string Locale { get; set; } = string.Empty;
        public ObservableCollection<BindingRow> Bindings { get; } = new();

        public static GroupRow FromModel(GenerationGroupConfiguration model)
        {
            var row = new GroupRow { Id = model.Id, ProfileId = model.ProfileId, Locale = model.Locale };
            foreach ((string output, string columnName) in model.Bindings)
            {
                row.Bindings.Add(new BindingRow { Output = output, ColumnName = columnName });
            }

            return row;
        }
    }

    public sealed class BindingRow
    {
        public string Output { get; set; } = string.Empty;
        public string ColumnName { get; set; } = string.Empty;
    }
}
