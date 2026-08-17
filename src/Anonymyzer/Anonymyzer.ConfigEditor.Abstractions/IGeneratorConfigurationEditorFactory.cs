namespace Anonymyzer.ConfigEditor.Abstractions;

using Newtonsoft.Json.Linq;

public interface IGeneratorConfigurationEditorFactory
{
    string GeneratorType { get; }

    string GeneratorVersion { get; }

    IGeneratorConfigurationEditor Create(JObject options);
}
