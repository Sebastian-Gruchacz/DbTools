namespace Anonymyzer.ConfigEditor;

using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Anonymyzer.ConfigEditor.Abstractions;
using Anonymyzer.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public partial class GeneratorProfilesWindow : Window
{
    private readonly List<GeneratorProfileConfiguration> _target;
    private readonly ObservableCollection<GeneratorProfileRow> _rows;
    private readonly IReadOnlyDictionary<string, IGeneratorConfigurationEditorFactory> _editorFactories;
    private readonly IReadOnlyList<GeneratorProfileConfiguration> _profileTemplates;

    public GeneratorProfilesWindow(
        List<GeneratorProfileConfiguration> profiles,
        IEnumerable<IGeneratorConfigurationEditorFactory> editorFactories,
        IReadOnlyList<GeneratorProfileConfiguration> profileTemplates)
    {
        InitializeComponent();
        _target = profiles;
        _rows = new ObservableCollection<GeneratorProfileRow>(profiles.Select(GeneratorProfileRow.FromModel));
        _editorFactories = editorFactories.ToDictionary(
            factory => BuildFactoryKey(factory.GeneratorType, factory.GeneratorVersion),
            StringComparer.OrdinalIgnoreCase);
        _profileTemplates = profileTemplates;
        DataContext = _rows;
    }

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button)
        {
            return;
        }

        var menu = new ContextMenu
        {
            PlacementTarget = button,
            Placement = PlacementMode.Bottom
        };
        foreach (GeneratorProfileConfiguration template in _profileTemplates.OrderBy(profile => profile.DisplayName))
        {
            var item = new MenuItem { Header = template.DisplayName };
            item.Click += (_, _) => AddProfile(GeneratorProfileRow.FromModel(template));
            menu.Items.Add(item);
        }

        menu.Items.Add(new Separator());
        var blankItem = new MenuItem { Header = "Blank profile..." };
        blankItem.Click += (_, _) => AddProfile(new GeneratorProfileRow
        {
            Id = "NewProfile",
            DisplayName = "New profile",
            OptionsJson = "{}"
        });
        menu.Items.Add(blankItem);

        button.ContextMenu = menu;
        menu.IsOpen = true;
    }

    private void AddProfile(GeneratorProfileRow row)
    {
        row.Id = CreateUniqueId(row.Id);

        _rows.Add(row);
        ProfilesGrid.SelectedItem = row;
        ProfilesGrid.ScrollIntoView(row);
    }

    private string CreateUniqueId(string requestedId)
    {
        string baseId = string.IsNullOrWhiteSpace(requestedId) ? "NewProfile" : requestedId;
        string candidate = baseId;
        int suffix = 2;
        while (_rows.Any(row => row.Id.Equals(candidate, StringComparison.OrdinalIgnoreCase)))
        {
            candidate = $"{baseId}:{suffix}";
            suffix++;
        }

        return candidate;
    }

    private void Remove_Click(object sender, RoutedEventArgs e)
    {
        if (ProfilesGrid.SelectedItem is GeneratorProfileRow row)
        {
            _rows.Remove(row);
        }
    }

    private void Configure_Click(object sender, RoutedEventArgs e)
    {
        ProfilesGrid.CommitEdit(DataGridEditingUnit.Cell, exitEditingMode: true);
        ProfilesGrid.CommitEdit(DataGridEditingUnit.Row, exitEditingMode: true);

        if (ProfilesGrid.SelectedItem is not GeneratorProfileRow row)
        {
            return;
        }

        string factoryKey = BuildFactoryKey(row.GeneratorType, row.GeneratorVersion);
        if (!_editorFactories.TryGetValue(factoryKey, out IGeneratorConfigurationEditorFactory? factory))
        {
            MessageBox.Show(
                this,
                "This generator version does not provide a dedicated editor. Edit Options JSON directly.",
                "No dedicated editor",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        try
        {
            IGeneratorConfigurationEditor editor = factory.Create(JObject.Parse(row.OptionsJson));
            var dialog = new GeneratorConfigurationWindow(editor) { Owner = this };
            if (dialog.ShowDialog() == true && dialog.SavedOptions is not null)
            {
                row.OptionsJson = dialog.SavedOptions.ToString(Formatting.None);
                ProfilesGrid.Items.Refresh();
            }
        }
        catch (JsonException exception)
        {
            MessageBox.Show(this, exception.Message, "Invalid options JSON", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            List<GeneratorProfileConfiguration> validated = _rows.Select(row => row.ToModel()).ToList();
            string? duplicateId = validated.GroupBy(profile => profile.Id, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault(group => group.Count() > 1)?.Key;
            if (duplicateId is not null)
            {
                throw new InvalidOperationException($"Profile id '{duplicateId}' is duplicated.");
            }

            _target.Clear();
            _target.AddRange(validated);
            DialogResult = true;
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException)
        {
            MessageBox.Show(this, exception.Message, "Invalid profile", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private sealed class GeneratorProfileRow
    {
        public string Id { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string GeneratorType { get; set; } = string.Empty;
        public string GeneratorVersion { get; set; } = string.Empty;
        public string Locale { get; set; } = string.Empty;
        public string Origin { get; set; } = string.Empty;
        public string OptionsJson { get; set; } = "{}";

        public static GeneratorProfileRow FromModel(GeneratorProfileConfiguration model)
        {
            return new GeneratorProfileRow
            {
                Id = model.Id,
                DisplayName = model.DisplayName,
                GeneratorType = model.GeneratorType,
                GeneratorVersion = model.GeneratorVersion,
                Locale = model.Locale,
                Origin = model.Origin,
                OptionsJson = model.Options.ToString(Formatting.None)
            };
        }

        public GeneratorProfileConfiguration ToModel()
        {
            if (string.IsNullOrWhiteSpace(Id))
            {
                throw new InvalidOperationException("Profile id is required.");
            }

            if (string.IsNullOrWhiteSpace(GeneratorType))
            {
                throw new InvalidOperationException($"Generator type is required for profile '{Id}'.");
            }

            if (string.IsNullOrWhiteSpace(GeneratorVersion))
            {
                throw new InvalidOperationException($"Generator version is required for profile '{Id}'.");
            }

            return new GeneratorProfileConfiguration
            {
                Id = Id.Trim(),
                DisplayName = DisplayName.Trim(),
                GeneratorType = GeneratorType.Trim(),
                GeneratorVersion = GeneratorVersion.Trim(),
                Locale = Locale.Trim(),
                Origin = string.IsNullOrWhiteSpace(Origin) ? "User" : Origin.Trim(),
                Options = JObject.Parse(OptionsJson)
            };
        }
    }

    private static string BuildFactoryKey(string generatorType, string generatorVersion)
    {
        return $"{generatorType.Trim()}@{generatorVersion.Trim()}";
    }
}
