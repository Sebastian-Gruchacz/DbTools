namespace Anonymyzer.Generators.Person.Wpf;

using Anonymyzer.ConfigEditor.Abstractions;
using Newtonsoft.Json.Linq;

public sealed class GenderGeneratorEditorFactory : IGeneratorConfigurationEditorFactory
{
    public string GeneratorType => GenderGenerator.GeneratorType;

    public string GeneratorVersion => GenderGenerator.GeneratorVersion;

    public IGeneratorConfigurationEditor Create(JObject options) => new GenderGeneratorEditor(options);
}
