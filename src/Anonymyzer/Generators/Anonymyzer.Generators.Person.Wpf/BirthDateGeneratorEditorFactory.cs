namespace Anonymyzer.Generators.Person.Wpf;

using Anonymyzer.ConfigEditor.Abstractions;
using Newtonsoft.Json.Linq;

public sealed class BirthDateGeneratorEditorFactory : IGeneratorConfigurationEditorFactory
{
    public string GeneratorType => BirthDateGenerator.GeneratorType;

    public string GeneratorVersion => BirthDateGenerator.GeneratorVersion;

    public IGeneratorConfigurationEditor Create(JObject options) => new BirthDateGeneratorEditor(options);
}
