namespace Anonymyzer.Generators.Simple.Wpf;

using Anonymyzer.ConfigEditor.Abstractions;
using Newtonsoft.Json.Linq;

public sealed class SequentialTextGeneratorEditorFactory : IGeneratorConfigurationEditorFactory
{
    public string GeneratorType => SequentialTextGenerator.GeneratorType;

    public string GeneratorVersion => SequentialTextGenerator.GeneratorVersion;

    public IGeneratorConfigurationEditor Create(JObject options) => new SequentialTextGeneratorEditor(options);
}
