namespace Anonymyzer.Generators.Simple.Wpf;

using Anonymyzer.ConfigEditor.Abstractions;
using Newtonsoft.Json.Linq;

public sealed class FixedTextGeneratorEditorFactory : IGeneratorConfigurationEditorFactory
{
    public string GeneratorType => FixedTextGenerator.GeneratorType;

    public string GeneratorVersion => FixedTextGenerator.GeneratorVersion;

    public IGeneratorConfigurationEditor Create(JObject options) => new FixedTextGeneratorEditor(options);
}
