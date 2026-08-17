namespace Anonymyzer.ConfigEditor.Abstractions;

using System.Windows;
using Newtonsoft.Json.Linq;

public interface IGeneratorConfigurationEditor
{
    FrameworkElement View { get; }

    IReadOnlyList<string> Validate();

    JObject Save();
}
