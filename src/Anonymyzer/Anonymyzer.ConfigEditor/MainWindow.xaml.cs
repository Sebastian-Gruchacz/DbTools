namespace Anonymyzer.ConfigEditor;

using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Anonymyzer.ConfigEditor.Infrastructure;
using Anonymyzer.ConfigEditor.ViewModels;
using Anonymyzer.ConfigEditor.Abstractions;
using Anonymyzer.Configuration;
using Anonymyzer.DatabaseAccess;
using Anonymyzer.Generators.Person.Wpf;
using Anonymyzer.Generators.Simple.Wpf;
using Microsoft.Win32;

public partial class MainWindow : Window
{
    private int _sampleWindowOffset;
    private readonly ConfigurationFileService _fileService = new();
    private readonly EditorViewModel _viewModel = new();
    private readonly GeneratorCatalog _generatorCatalog = new();
    private readonly GeneratorPreviewService _previewService;
    private readonly IGeneratorConfigurationEditorFactory[] _generatorEditors =
    {
        new ShufflingTextGeneratorEditorFactory(),
        new PersonIdentityGeneratorEditorFactory()
    };

    public MainWindow()
    {
        _previewService = new GeneratorPreviewService(_generatorCatalog);
        InitializeComponent();
        DataContext = _viewModel;
        _viewModel.Load(_generatorCatalog.CreateNewConfiguration(), null);
    }

    private void New_Click(object sender, RoutedEventArgs e)
    {
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

        RunFileOperation(() => _viewModel.Load(_fileService.Load(dialog.FileName), dialog.FileName));
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.CurrentPath is null)
        {
            SaveAs_Click(sender, e);
            return;
        }

        SaveTo(_viewModel.CurrentPath);
    }

    private void SaveAs_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Filter = "Anonymyzer configuration (*.json)|*.json|All files (*.*)|*.*",
            DefaultExt = ".json",
            AddExtension = true,
            FileName = _viewModel.CurrentPath is null ? "anonymyzer-config.json" : Path.GetFileName(_viewModel.CurrentPath)
        };

        if (dialog.ShowDialog(this) == true)
        {
            SaveTo(dialog.FileName);
        }
    }

    private void Profiles_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new GeneratorProfilesWindow(_viewModel.Configuration.GeneratorProfiles, _generatorEditors)
        {
            Owner = this
        };

        if (dialog.ShowDialog() == true)
        {
            _viewModel.RefreshProfiles();
            _viewModel.Status = "Generator profiles updated. Save the configuration to persist changes.";
        }
    }

    private void Groups_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.SelectedTable is null)
        {
            return;
        }

        var dialog = new GenerationGroupsWindow(
            _viewModel.SelectedTable.Model,
            _viewModel.Configuration.GeneratorProfiles,
            _generatorCatalog.Descriptors)
        {
            Owner = this
        };

        if (dialog.ShowDialog() == true)
        {
            _viewModel.RefreshTables();
            _viewModel.Status = "Generation groups updated. Save the configuration to persist changes.";
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
            _viewModel.Status = "Generating preview...";
            IReadOnlyDictionary<string, string> samples = await _previewService.GenerateAsync(
                table.Model,
                _viewModel.Configuration.GeneratorProfiles);
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
        if (table is null)
        {
            return;
        }

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
            table.Model.Columns.Add(new ColumnProcessingOptions
            {
                Ordinal = column.Ordinal,
                ColumnName = column.ColumnName,
                DataType = column.DataType,
                MaxLength = column.MaxLength,
                Unicode = column.Unicode
            });
        }

        table.Model.Columns = table.Model.Columns.OrderBy(column => column.Ordinal).ToList();
        _viewModel.RefreshTables();
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

    private void SaveTo(string path)
    {
        RunFileOperation(() =>
        {
            _fileService.Save(path, _viewModel.Configuration);
            _viewModel.SetCurrentPath(path);
        });
    }

    private void RunFileOperation(Action operation)
    {
        try
        {
            operation();
        }
        catch (Exception exception)
        {
            _viewModel.Status = exception.Message;
            MessageBox.Show(this, exception.Message, "Configuration error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
