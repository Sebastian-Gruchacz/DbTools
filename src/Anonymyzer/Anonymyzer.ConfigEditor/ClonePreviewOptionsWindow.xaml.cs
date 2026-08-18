namespace Anonymyzer.ConfigEditor;

using System.Windows;
using Anonymyzer.DatabaseAccess;

public partial class ClonePreviewOptionsWindow : Window
{
    public ClonePreviewOptionsWindow(string connectionEnvironmentVariable, int maximumRows)
    {
        InitializeComponent();
        ConnectionEnvironmentTextBox.Text = connectionEnvironmentVariable;
        MaximumRowsTextBox.Text = maximumRows.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    public string ConnectionEnvironmentVariable { get; private set; } = string.Empty;

    public int MaximumRows { get; private set; }

    private void Preview_Click(object sender, RoutedEventArgs e)
    {
        string connectionEnvironmentVariable = ConnectionEnvironmentTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(connectionEnvironmentVariable))
        {
            MessageBox.Show(this, "Connection environment variable is required.", "Invalid preview options");
            return;
        }

        if (!int.TryParse(MaximumRowsTextBox.Text, out int maximumRows)
            || maximumRows is < 2 or > LimitedGeneratorPreviewDataReader.MaximumPreviewRows)
        {
            MessageBox.Show(
                this,
                $"Sample rows must be between 2 and {LimitedGeneratorPreviewDataReader.MaximumPreviewRows}.",
                "Invalid preview options");
            return;
        }

        ConnectionEnvironmentVariable = connectionEnvironmentVariable;
        MaximumRows = maximumRows;
        DialogResult = true;
    }
}
