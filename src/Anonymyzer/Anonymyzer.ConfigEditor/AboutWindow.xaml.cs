namespace Anonymyzer.ConfigEditor;

using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Navigation;

public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();
        Assembly assembly = typeof(AboutWindow).Assembly;
        string version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                         ?? assembly.GetName().Version?.ToString()
                         ?? "unknown";
        VersionTextBlock.Text = $"Version: {version.Split('+')[0]}";
    }

    private void Link_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }
}
