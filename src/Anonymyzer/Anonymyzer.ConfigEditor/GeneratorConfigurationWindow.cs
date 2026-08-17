namespace Anonymyzer.ConfigEditor;

using System.Windows;
using System.Windows.Controls;
using Anonymyzer.ConfigEditor.Abstractions;
using Newtonsoft.Json.Linq;

internal sealed class GeneratorConfigurationWindow : Window
{
    private readonly IGeneratorConfigurationEditor _editor;

    public GeneratorConfigurationWindow(IGeneratorConfigurationEditor editor)
    {
        _editor = editor ?? throw new ArgumentNullException(nameof(editor));
        Title = "Generator configuration";
        Width = 560;
        Height = 360;
        MinWidth = 440;
        MinHeight = 280;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(12)
        };
        var saveButton = new Button { Content = "OK", Width = 90, IsDefault = true, Margin = new Thickness(0, 0, 8, 0) };
        saveButton.Click += Save_Click;
        var cancelButton = new Button { Content = "Cancel", Width = 90, IsCancel = true };
        buttons.Children.Add(saveButton);
        buttons.Children.Add(cancelButton);

        var layout = new DockPanel();
        DockPanel.SetDock(buttons, Dock.Bottom);
        layout.Children.Add(buttons);
        layout.Children.Add(editor.View);
        Content = layout;
    }

    public JObject? SavedOptions { get; private set; }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        IReadOnlyList<string> errors = _editor.Validate();
        if (errors.Count > 0)
        {
            MessageBox.Show(this, string.Join(Environment.NewLine, errors), "Invalid configuration", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        SavedOptions = _editor.Save();
        DialogResult = true;
    }
}
