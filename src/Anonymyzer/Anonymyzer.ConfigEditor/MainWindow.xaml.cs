namespace Anonymyzer.ConfigEditor;

using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Anonymyzer.ConfigEditor.Infrastructure;
using Anonymyzer.ConfigEditor.ViewModels;
using Anonymyzer.ConfigEditor.Abstractions;
using Anonymyzer.Configuration;
using Anonymyzer.Base.LanguagePacks;
using Anonymyzer.DatabaseAccess;
using Anonymyzer.Generators.Address.Wpf;
using Anonymyzer.Generators.Person.Wpf;
using Anonymyzer.Generators.Simple.Wpf;
using Anonymyzer.LanguagePack.English;
using Anonymyzer.LanguagePack.Polish;
using Microsoft.Win32;
using Newtonsoft.Json.Linq;

public partial class MainWindow : Window
{
    private int _sampleWindowOffset;
    private string _previewConnectionEnvironmentVariable = "ANONYMYZER_CONNECTION";
    private string _rescanConnectionEnvironmentVariable = "ANONYMYZER_CONNECTION";
    private int _previewMaximumRows = 10;
    private readonly ConfigurationFileService _fileService = new();
    private readonly EditorViewModel _viewModel = new();
    private readonly LanguagePackCatalog _languagePacks;
    private readonly LanguagePackInstallationService _languagePackInstallations;
    private readonly GeneratorCatalog _generatorCatalog;
    private readonly DatabaseRescanService _rescanService;
    private readonly GeneratorPreviewService _previewService;
    private readonly IGeneratorConfigurationEditorFactory[] _generatorEditors =
    {
        new ShufflingTextGeneratorEditorFactory(),
        new FixedTextGeneratorEditorFactory(),
        new JsonPathRedactorGeneratorEditorFactory(),
        new SequentialTextGeneratorEditorFactory(),
        new EmailAddressGeneratorEditorFactory(),
        new AccountLoginGeneratorEditorFactory(),
        new PhoneNumberGeneratorEditorFactory(),
        new UuidGeneratorEditorFactory(),
        new CompanyNameGeneratorEditorFactory(),
        new TaxIdentifierGeneratorEditorFactory(),
        new BankAccountGeneratorEditorFactory(),
        new BirthDateGeneratorEditorFactory(),
        new GenderGeneratorEditorFactory(),
        new NationalIdentifierGeneratorEditorFactory(),
        new PersonIdentityGeneratorEditorFactory(),
        new PostalAddressGeneratorEditorFactory()
    };

    public MainWindow()
    {
        _languagePackInstallations = new LanguagePackInstallationService(
        [
            new EnglishLanguagePack(),
            new PolishLanguagePack()
        ]);
        _languagePacks = new LanguagePackCatalog(_languagePackInstallations.ActivePacks);
        _generatorCatalog = new GeneratorCatalog(_languagePacks);
        _rescanService = new DatabaseRescanService(_languagePacks);
        _previewService = new GeneratorPreviewService(_generatorCatalog);
        InitializeComponent();
        DataContext = _viewModel;
        _viewModel.Load(_generatorCatalog.CreateNewConfiguration(), null);
    }

    private void New_Click(object sender, RoutedEventArgs e)
    {
        if (!ConfirmSaveChanges())
        {
            return;
        }

        _viewModel.Load(_generatorCatalog.CreateNewConfiguration(), null);
    }

    private void Open_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Anonymyzer configuration (*.json)|*.json|All files (*.*)|*.*"
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        if (!ConfirmSaveChanges())
        {
            return;
        }

        RunFileOperation(() =>
        {
            AnonymizationConfiguration configuration = _fileService.Load(dialog.FileName);
            GeneratorProfileMergeResult merge = new GeneratorProfileMerger().Merge(
                configuration.GeneratorProfiles,
                _generatorCatalog.CreateDefaultProfiles());
            _viewModel.Load(configuration, dialog.FileName);
            if (merge.Changed)
            {
                _viewModel.MarkDirty();
                _viewModel.Status = $"Opened {dialog.FileName}; merged built-in profiles: " +
                                    $"{merge.AddedProfiles} added, {merge.UpdatedProfiles} updated, " +
                                    $"{merge.IdCollisions} id collision(s). Save to persist the merge.";
            }
        });
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        TrySaveDocument();
    }

    private async void Rescan_Click(object sender, RoutedEventArgs e)
    {
        CommitPendingGridEdits();
        var dialog = new RescanOptionsWindow(_rescanConnectionEnvironmentVariable) { Owner = this };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        _rescanConnectionEnvironmentVariable = dialog.ConnectionEnvironmentVariable;
        try
        {
            _viewModel.Status = "Rescanning detached clone...";
            IReadOnlyList<TableProcessingOptions> freshTables = await _rescanService.ScanAsync(
                _viewModel.Configuration,
                _rescanConnectionEnvironmentVariable);
            DatabaseRescanMergeResult result = new DatabaseRescanMerger().Merge(
                _viewModel.Configuration,
                freshTables);
            _viewModel.RefreshTables();
            _viewModel.MarkDirty();
            string summary = $"Rescan complete: {result.AddedTables} table(s) and {result.AddedColumns} column(s) added; " +
                             $"{result.RefreshedColumns} column(s) refreshed; {result.PreservedSelections} selection(s) preserved; " +
                             $"{result.MissingTables} table(s) and {result.MissingColumns} column(s) retained as missing.";
            _viewModel.Status = summary;
            MessageBox.Show(this, summary, "Database rescan", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception exception)
        {
            _viewModel.Status = exception.Message;
            MessageBox.Show(this, exception.Message, "Rescan error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void SaveAs_Click(object sender, RoutedEventArgs e)
    {
        CommitPendingGridEdits();
        TrySaveAs();
    }

    private bool TrySaveAs()
    {
        var dialog = new SaveFileDialog
        {
            Filter = "Anonymyzer configuration (*.json)|*.json|All files (*.*)|*.*",
            DefaultExt = ".json",
            AddExtension = true,
            FileName = _viewModel.CurrentPath is null ? "anonymyzer-config.json" : Path.GetFileName(_viewModel.CurrentPath)
        };

        return dialog.ShowDialog(this) == true && SaveTo(dialog.FileName);
    }

    private void Profiles_Click(object sender, RoutedEventArgs e)
    {
        CommitPendingGridEdits();
        JToken originalProfiles = JToken.FromObject(_viewModel.Configuration.GeneratorProfiles);
        var dialog = new GeneratorProfilesWindow(
            _viewModel.Configuration.GeneratorProfiles,
            _generatorEditors,
            _generatorCatalog.CreateDefaultProfiles())
        {
            Owner = this
        };

        if (dialog.ShowDialog() == true)
        {
            _viewModel.RefreshProfiles();
            if (!JToken.DeepEquals(originalProfiles, JToken.FromObject(_viewModel.Configuration.GeneratorProfiles)))
            {
                _viewModel.MarkDirty();
                _viewModel.Status = "Generator profiles updated. Save the configuration to persist changes.";
            }
        }
    }

    private void LanguagePacks_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new LanguagePacksWindow(_languagePackInstallations) { Owner = this };
        if (dialog.ShowDialog() == true && dialog.RestartRequired)
        {
            MessageBox.Show(
                this,
                "Language-pack changes were saved and will take effect after restarting Anonymyzer. " +
                "Existing document settings are not removed.",
                "Restart required",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
    }

    private void Groups_Click(object sender, RoutedEventArgs e)
    {
        CommitPendingGridEdits();
        if (_viewModel.SelectedTable is null)
        {
            return;
        }

        TableViewModel table = _viewModel.SelectedTable;
        JToken originalTable = JToken.FromObject(table.Model);
        var dialog = new GenerationGroupsWindow(
            table.Model,
            _viewModel.Configuration.GeneratorProfiles,
            _generatorCatalog.Descriptors)
        {
            Owner = this
        };

        if (dialog.ShowDialog() == true)
        {
            bool changed = !JToken.DeepEquals(originalTable, JToken.FromObject(table.Model));
            _viewModel.RefreshTables();
            if (changed)
            {
                _viewModel.MarkDirty();
                _viewModel.Status = "Generation groups updated. Save the configuration to persist changes.";
            }
        }
    }

    private async void RefreshSample_Click(object sender, RoutedEventArgs e)
    {
        TableViewModel? table = _viewModel.SelectedTable;
        if (table is null)
        {
            return;
        }

        try
        {
            if (_previewService.RequiresCloneData(
                    table.Model,
                    _viewModel.Configuration.GeneratorProfiles))
            {
                var dialog = new ClonePreviewOptionsWindow(
                    _previewConnectionEnvironmentVariable,
                    _previewMaximumRows)
                {
                    Owner = this
                };
                if (dialog.ShowDialog() != true)
                {
                    return;
                }

                _previewConnectionEnvironmentVariable = dialog.ConnectionEnvironmentVariable;
                _previewMaximumRows = dialog.MaximumRows;
            }

            _viewModel.Status = "Generating preview...";
            IReadOnlyDictionary<string, string> samples = await _previewService.GenerateAsync(
                _viewModel.Configuration,
                table.Model,
                _viewModel.Configuration.GeneratorProfiles,
                _previewConnectionEnvironmentVariable,
                _previewMaximumRows);
            table.ApplySamples(samples);
            _viewModel.Status = "Preview generated without modifying the database.";
        }
        catch (Exception exception)
        {
            _viewModel.Status = exception.Message;
            MessageBox.Show(this, exception.Message, "Preview error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void AddColumns_Click(object sender, RoutedEventArgs e)
    {
        TableViewModel? table = _viewModel.SelectedTable;
        if (table is null || sender is not Button button)
        {
            return;
        }

        var menu = new ContextMenu
        {
            PlacementTarget = button,
            Placement = PlacementMode.Bottom
        };

        foreach (ColumnViewModel column in table.HiddenColumns.OrderBy(column => column.Ordinal))
        {
            var item = new MenuItem
            {
                Header = $"{column.ColumnName}  ({column.TypeDisplay})",
                ToolTip = $"Column #{column.Ordinal} from the saved analysis"
            };
            item.Click += (_, _) => RevealColumn(table, column);
            menu.Items.Add(item);
        }

        if (table.HiddenColumns.Count == 0)
        {
            menu.Items.Add(new MenuItem
            {
                Header = "No hidden analyzed columns",
                IsEnabled = false
            });
        }

        menu.Items.Add(new Separator());
        var loadItem = new MenuItem { Header = "Load more from database..." };
        loadItem.Click += (_, _) => LoadColumnsFromDatabase(table);
        menu.Items.Add(loadItem);

        button.ContextMenu = menu;
        menu.IsOpen = true;
    }

    private void RevealColumn(TableViewModel table, ColumnViewModel column)
    {
        table.RevealColumn(column);
        _viewModel.Status = $"Column {column.ColumnName} is now visible for configuration.";
    }

    private void LoadColumnsFromDatabase(TableViewModel table)
    {
        var dialog = new AddColumnsWindow(_viewModel.Configuration, table.Model)
        {
            Owner = this
        };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        foreach (AvailableColumn column in dialog.SelectedColumns)
        {
            table.AddColumn(new ColumnProcessingOptions
            {
                Ordinal = column.Ordinal,
                ColumnName = column.ColumnName,
                DataType = column.DataType,
                MaxLength = column.MaxLength,
                Unicode = column.Unicode
            });
        }

        _viewModel.Status = $"Added {dialog.SelectedColumns.Count} column(s). Save the configuration to persist changes.";
    }

    private void ViewValues_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.SelectedTable is null
            || sender is not FrameworkElement { DataContext: ColumnViewModel column })
        {
            return;
        }

        var window = new ColumnSamplesWindow(
            _viewModel.Configuration,
            _viewModel.SelectedTable.Model,
            column.Model)
        {
            Owner = this,
            WindowStartupLocation = WindowStartupLocation.Manual,
            Left = Left + ActualWidth + 8 + _sampleWindowOffset,
            Top = Top + 24 + _sampleWindowOffset
        };
        _sampleWindowOffset = (_sampleWindowOffset + 24) % 144;
        window.Show();
    }

    private void SemanticRole_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: ColumnViewModel column } button)
        {
            return;
        }

        var menu = new ContextMenu
        {
            PlacementTarget = button,
            Placement = PlacementMode.Bottom
        };

        foreach (SemanticRoleGroup group in column.SemanticRoleGroups)
        {
            var groupItem = new MenuItem { Header = group.DisplayName };
            foreach (SemanticRoleOption option in group.Options)
            {
                var optionItem = new MenuItem
                {
                    Header = option.DisplayName,
                    IsCheckable = true,
                    IsChecked = option.Value == column.SemanticRoleValue
                };
                optionItem.Click += (_, _) => column.SelectSemanticRole(option);
                groupItem.Items.Add(optionItem);
            }

            menu.Items.Add(groupItem);
        }

        button.ContextMenu = menu;
        menu.IsOpen = true;
    }

    private void Exit_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void TableLegend_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show(
            this,
            "● in the table list means that the latest saved analysis found at least one anonymization candidate.\n" +
            "The adjacent number is the candidate-column count. An empty marker means no automatic candidate was found.\n\n" +
            "The same ● in the column grid marks an automatically detected candidate; hover it to see the suggested " +
            "semantic role, confidence and matched rule. Markers are suggestions only and never enable anonymization.\n\n" +
            "A blue ◆ marks a table or column containing explicit operator choices. Hover it to see which settings " +
            "are protected from a future database rescan.\n\n" +
            "A red ⚠ means that a saved table or column was not found during the latest rescan. Its configuration " +
            "is retained for review and is never silently deleted.",
            "Table and column markings",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void Documentation_Click(object sender, RoutedEventArgs e) =>
        OpenWebPage("https://github.com/Sebastian-Gruchacz/DbTools#readme");

    private void About_Click(object sender, RoutedEventArgs e)
    {
        new AboutWindow { Owner = this }.ShowDialog();
    }

    private static void OpenWebPage(string url)
    {
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        e.Cancel = !ConfirmSaveChanges();
    }

    private bool ConfirmSaveChanges()
    {
        CommitPendingGridEdits();
        if (!_viewModel.IsDirty)
        {
            return true;
        }

        MessageBoxResult result = MessageBox.Show(
            this,
            $"Save changes to '{_viewModel.DocumentDisplayName}' before continuing?",
            "Unsaved changes",
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Warning);
        return result switch
        {
            MessageBoxResult.Yes => TrySaveDocument(),
            MessageBoxResult.No => true,
            _ => false
        };
    }

    private bool TrySaveDocument()
    {
        CommitPendingGridEdits();
        return _viewModel.CurrentPath is null
            ? TrySaveAs()
            : SaveTo(_viewModel.CurrentPath);
    }

    private void CommitPendingGridEdits()
    {
        ColumnsGrid.CommitEdit(DataGridEditingUnit.Cell, exitEditingMode: true);
        ColumnsGrid.CommitEdit(DataGridEditingUnit.Row, exitEditingMode: true);
    }

    private bool SaveTo(string path)
    {
        return RunFileOperation(() =>
        {
            _fileService.Save(path, _viewModel.Configuration);
            _viewModel.SetCurrentPath(path);
        });
    }

    private bool RunFileOperation(Action operation)
    {
        try
        {
            operation();
            return true;
        }
        catch (Exception exception)
        {
            _viewModel.Status = exception.Message;
            MessageBox.Show(this, exception.Message, "Configuration error", MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }
    }
}
