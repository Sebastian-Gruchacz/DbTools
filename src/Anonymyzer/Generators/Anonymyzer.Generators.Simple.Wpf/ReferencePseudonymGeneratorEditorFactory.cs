namespace Anonymyzer.Generators.Simple.Wpf;

using Anonymyzer.ConfigEditor.Abstractions;
using Newtonsoft.Json.Linq;

public sealed class ReferencePseudonymGeneratorEditorFactory : IGeneratorConfigurationEditorFactory
{
    public string GeneratorType => ReferencePseudonymGenerator.GeneratorType;

    public string GeneratorVersion => ReferencePseudonymGenerator.GeneratorVersion;

    public IGeneratorConfigurationEditor Create(JObject options) => new ReferencePseudonymGeneratorEditor(options);
}
