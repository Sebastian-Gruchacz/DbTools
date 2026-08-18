namespace Anonymyzer.Generators.Simple.Wpf;

using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using Anonymyzer.ConfigEditor.Abstractions;
using Newtonsoft.Json.Linq;

public partial class JsonPathRedactorGeneratorEditor : UserControl, IGeneratorConfigurationEditor
{
    private readonly JsonPathRedactorGeneratorConfigurationCodec _codec = new();
    private readonly ObservableCollection<JsonPathRedactionRuleConfiguration> _rules;

    public JsonPathRedactorGeneratorEditor(JObject options)
    {
        InitializeComponent();
        var configuration = (JsonPathRedactorGeneratorConfiguration)_codec.Deserialize(options);
        _rules = new ObservableCollection<JsonPathRedactionRuleConfiguration>(
            configuration.Rules.Select(CloneRule));
        RulesGrid.ItemsSource = _rules;
        RequireEveryPathCheckBox.IsChecked = configuration.RequireEveryPath;
    }

    public FrameworkElement View => this;

    public IReadOnlyList<string> Validate()
    {
        CommitPendingEdits();
        return _codec.Validate(BuildConfiguration());
    }

    public JObject Save()
    {
        CommitPendingEdits();
        JsonPathRedactorGeneratorConfiguration configuration = BuildConfiguration();
        IReadOnlyList<string> errors = _codec.Validate(configuration);
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, errors));
        }

        return _codec.Serialize(configuration);
    }

    private void AddRule_Click(object sender, RoutedEventArgs e)
    {
        var rule = new JsonPathRedactionRuleConfiguration
        {
            Path = "$/sensitive",
            ReplacementJson = "\"REDACTED\""
        };
        _rules.Add(rule);
        RulesGrid.SelectedItem = rule;
        RulesGrid.ScrollIntoView(rule);
        RulesGrid.BeginEdit();
    }

    private void RemoveRule_Click(object sender, RoutedEventArgs e)
    {
        if (RulesGrid.SelectedItem is JsonPathRedactionRuleConfiguration rule)
        {
            _rules.Remove(rule);
        }
    }

    private JsonPathRedactorGeneratorConfiguration BuildConfiguration() => new()
    {
        Rules = _rules.Select(CloneRule).ToList(),
        RequireEveryPath = RequireEveryPathCheckBox.IsChecked == true
    };

    private void CommitPendingEdits()
    {
        RulesGrid.CommitEdit(DataGridEditingUnit.Cell, exitEditingMode: true);
        RulesGrid.CommitEdit(DataGridEditingUnit.Row, exitEditingMode: true);
    }

    private static JsonPathRedactionRuleConfiguration CloneRule(JsonPathRedactionRuleConfiguration rule) => new()
    {
        Path = rule.Path,
        ReplacementJson = rule.ReplacementJson
    };
}
