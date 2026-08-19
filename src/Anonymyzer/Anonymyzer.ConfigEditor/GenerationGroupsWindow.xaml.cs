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
    private readonly IReadOnlyList<GeneratorProfileConfiguration> _multiOutputProfiles;
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
        _multiOutputProfiles = profiles
            .Where(profile => GetDescriptor(profile)?.Outputs.Count > 1)
            .ToArray();
        Groups = new ObservableCollection<GroupRow>(table.GenerationGroups.Select(GroupRow.FromModel));
        ProfileIds = _multiOutputProfiles
            .Select(profile => profile.Id)
            .Concat(table.GenerationGroups.Select(group => group.ProfileId))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value)
            .ToArray();
        ColumnOptions = table.Columns
            .OrderBy(column => column.Ordinal)
            .Select(column => new ColumnOption(
                column.ColumnName,
                $"{column.ColumnName} — {column.DataType}" + (column.Enabled ? string.Empty : " [disabled]")))
            .ToArray();
        DataContext = this;
        SelectedGroup = Groups.FirstOrDefault();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<GroupRow> Groups { get; }
    public IReadOnlyList<string> ProfileIds { get; }
    public IReadOnlyList<ColumnOption> ColumnOptions { get; }
    public IReadOnlyList<OutputOption> SelectedOutputOptions => SelectedGroup is null
        ? Array.Empty<OutputOption>()
        : GetDescriptor(SelectedGroup.ProfileId)?.Outputs
            .Select(output => new OutputOption(
                output.Name,
                $"{output.Name} — {output.DisplayName}" + (output.Required ? " [required]" : " [optional]")))
            .ToArray()
          ?? Array.Empty<OutputOption>();

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
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedOutputOptions)));
            BindingsGrid.ItemsSource = value?.Bindings;
        }
    }

    private void GroupsGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        SelectedGroup = GroupsGrid.SelectedItem as GroupRow;
    }

    private void AddGroup_Click(object sender, RoutedEventArgs e)
    {
        GeneratorProfileConfiguration? profile = _multiOutputProfiles.FirstOrDefault(item =>
            item.GeneratorType.Equals("PersonIdentity", StringComparison.OrdinalIgnoreCase))
            ?? _multiOutputProfiles.FirstOrDefault();
        if (profile is null)
        {
            MessageBox.Show(
                this,
                "Create a profile for a multi-output generator, such as PersonIdentity or PostalAddress, first.",
                "No group profile",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

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

    private void Profile_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded
            || sender is not ComboBox { DataContext: GroupRow row, SelectedItem: string profileId })
        {
            return;
        }

        GeneratorProfileConfiguration? profile = _profiles.FirstOrDefault(item =>
            item.Id.Equals(profileId, StringComparison.OrdinalIgnoreCase));
        GeneratorDescriptor? descriptor = profile is null ? null : GetDescriptor(profile);
        if (profile is null || descriptor is null || descriptor.Outputs.Count <= 1)
        {
            return;
        }

        row.ProfileId = profile.Id;
        row.Locale = profile.Locale;
        foreach (BindingRow binding in row.Bindings
                     .Where(binding => descriptor.Outputs.All(output =>
                         !output.Name.Equals(binding.Output, StringComparison.OrdinalIgnoreCase)))
                     .ToArray())
        {
            row.Bindings.Remove(binding);
        }

        AddMissingBindings(row);
        if (ReferenceEquals(SelectedGroup, row))
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedOutputOptions)));
        }

        GroupsGrid.Items.Refresh();
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
        string columnName = ColumnOptions.Select(option => option.Name).FirstOrDefault(name =>
            Groups.SelectMany(group => group.Bindings).All(binding =>
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
            string? activationWarning = BuildActivationWarning(groups);
            if (activationWarning is not null
                && MessageBox.Show(
                    this,
                    activationWarning + Environment.NewLine + Environment.NewLine +
                    "Save the group configuration anyway?",
                    "Inactive group bindings",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning) != MessageBoxResult.Yes)
            {
                return;
            }

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

                ColumnProcessingOptions? column = _table.Columns.FirstOrDefault(candidate =>
                    candidate.ColumnName.Equals(binding.ColumnName, StringComparison.OrdinalIgnoreCase));
                if (column is null)
                {
                    throw new InvalidOperationException($"Group '{row.Id}' references missing column '{binding.ColumnName}'.");
                }

                if (!GeneratorColumnCompatibility.Supports(descriptor, column.DataType))
                {
                    throw new InvalidOperationException(
                        $"Generator {descriptor.Type} supports " +
                        $"{GeneratorColumnCompatibility.DescribeSupportedTypes(descriptor)}, but column " +
                        $"'{column.ColumnName}' is {column.DataType}.");
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

    private string? BuildActivationWarning(IEnumerable<GenerationGroupConfiguration> groups)
    {
        string[] disabledColumns = groups
            .SelectMany(group => group.Bindings.Values)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(columnName => _table.Columns.Any(column =>
                column.ColumnName.Equals(columnName, StringComparison.OrdinalIgnoreCase) && !column.Enabled))
            .OrderBy(columnName => columnName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (_table.Enabled && disabledColumns.Length == 0)
        {
            return null;
        }

        var messages = new List<string>();
        if (!_table.Enabled)
        {
            messages.Add($"Table {_table.SchemaName}.{_table.TableName} is not enabled.");
        }

        if (disabledColumns.Length > 0)
        {
            messages.Add($"Bound columns not enabled: {string.Join(", ", disabledColumns)}.");
        }

        messages.Add(
            "Inactive bindings are not planned; an inactive required output makes dry-run fail.");
        return string.Join(Environment.NewLine, messages);
    }

    private void ApplyGroups(List<GenerationGroupConfiguration> groups)
    {
        _table.GenerationGroups.Clear();
        _table.GenerationGroups.AddRange(groups);
        foreach (ColumnProcessingOptions column in _table.Columns)
        {
            if (!string.IsNullOrWhiteSpace(column.GenerationGroupId))
            {
                column.OperatorOverrides.GenerationGroup = true;
            }

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
                column.OperatorOverrides.GenerationGroup = true;
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
            if (row.Bindings.Any(binding =>
                    binding.Output.Equals(output.Name, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

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

    public sealed record OutputOption(string Name, string DisplayName);

    public sealed record ColumnOption(string Name, string DisplayName);
}
