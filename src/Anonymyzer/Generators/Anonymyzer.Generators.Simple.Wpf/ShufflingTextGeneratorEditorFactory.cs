namespace Anonymyzer.Generators.Simple.Wpf;

using Anonymyzer.ConfigEditor.Abstractions;
using Newtonsoft.Json.Linq;

public sealed class ShufflingTextGeneratorEditorFactory : IGeneratorConfigurationEditorFactory
{
    public string GeneratorType => ShufflingTextGenerator.GeneratorType;

    public string GeneratorVersion => ShufflingTextGenerator.GeneratorVersion;

    public IGeneratorConfigurationEditor Create(JObject options)
    {
        return new ShufflingTextGeneratorEditor(options);
    }
}
