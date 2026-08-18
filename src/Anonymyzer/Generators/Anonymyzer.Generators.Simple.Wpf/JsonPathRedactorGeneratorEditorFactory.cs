namespace Anonymyzer.Generators.Simple.Wpf;

using Anonymyzer.ConfigEditor.Abstractions;
using Newtonsoft.Json.Linq;

public sealed class JsonPathRedactorGeneratorEditorFactory : IGeneratorConfigurationEditorFactory
{
    public string GeneratorType => JsonPathRedactorGenerator.GeneratorType;

    public string GeneratorVersion => JsonPathRedactorGenerator.GeneratorVersion;

    public IGeneratorConfigurationEditor Create(JObject options) => new JsonPathRedactorGeneratorEditor(options);
}
