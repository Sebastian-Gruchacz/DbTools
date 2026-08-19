namespace Anonymyzer.ConfigEditor;

using System.Collections.ObjectModel;
using System.Windows;
using Anonymyzer.Base.LanguagePacks;
using Microsoft.Win32;

public partial class LanguagePacksWindow : Window
{
    private readonly LanguagePackInstallationService _service;

    public LanguagePacksWindow(LanguagePackInstallationService service)
    {
        _service = service;
        Packs = new ObservableCollection<LanguagePackRow>(service.Installations.Select(LanguagePackRow.From));
        Warnings = service.LoadWarnings.Count == 0
            ? string.Empty
            : "Some installed packages could not be loaded: " + string.Join(" | ", service.LoadWarnings);
        InitializeComponent();
        DataContext = this;
    }

    public ObservableCollection<LanguagePackRow> Packs { get; }

    public string Warnings { get; }

    public bool RestartRequired { get; private set; }

    private void Install_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Anonymyzer language pack (*.dll)|*.dll|All files (*.*)|*.*"
        };
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        try
        {
            LanguagePackInstallation installation = _service.Install(dialog.FileName);
            Packs.Add(LanguagePackRow.From(installation));
            RestartRequired = true;
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, exception.Message, "Language-pack installation", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Apply_Click(object sender, RoutedEventArgs e)
    {
        foreach (LanguagePackRow row in Packs)
        {
            RestartRequired |= _service.SetEnabled(row.Id, row.IsEnabled);
        }

        DialogResult = true;
    }

    public sealed class LanguagePackRow
    {
        public required string Id { get; init; }
        public required string DisplayName { get; init; }
        public required string Version { get; init; }
        public required string Locales { get; init; }
        public required string Origin { get; init; }
        public bool IsEnabled { get; set; }

        public static LanguagePackRow From(LanguagePackInstallation installation) => new()
        {
            Id = installation.Pack.Descriptor.Id,
            DisplayName = installation.Pack.Descriptor.DisplayName,
            Version = installation.Pack.Descriptor.Version,
            Locales = string.Join(", ", installation.Pack.Descriptor.Locales),
            Origin = installation.Origin,
            IsEnabled = installation.IsEnabled
        };
    }
}
