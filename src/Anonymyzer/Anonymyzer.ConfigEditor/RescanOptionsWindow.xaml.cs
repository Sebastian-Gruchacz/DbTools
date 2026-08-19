namespace Anonymyzer.ConfigEditor;

using System.Windows;

public partial class RescanOptionsWindow : Window
{
    public RescanOptionsWindow(string connectionEnvironmentVariable)
    {
        InitializeComponent();
        ConnectionEnvironmentTextBox.Text = connectionEnvironmentVariable;
    }

    public string ConnectionEnvironmentVariable { get; private set; } = string.Empty;

    private void Rescan_Click(object sender, RoutedEventArgs e)
    {
        string value = ConnectionEnvironmentTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(value))
        {
            MessageBox.Show(this, "Connection environment variable is required.", "Invalid rescan options");
            return;
        }

        ConnectionEnvironmentVariable = value;
        DialogResult = true;
    }
}
