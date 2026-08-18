namespace Anonymyzer.Generators.Simple.Wpf;

using Anonymyzer.ConfigEditor.Abstractions;
using Newtonsoft.Json.Linq;

public sealed class UuidGeneratorEditorFactory : IGeneratorConfigurationEditorFactory
{
    public string GeneratorType => UuidGenerator.GeneratorType;

    public string GeneratorVersion => UuidGenerator.GeneratorVersion;

    public IGeneratorConfigurationEditor Create(JObject options) => new UuidGeneratorEditor(options);
}
