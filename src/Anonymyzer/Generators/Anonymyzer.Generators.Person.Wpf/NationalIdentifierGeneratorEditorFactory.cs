namespace Anonymyzer.Generators.Person.Wpf;

using Anonymyzer.ConfigEditor.Abstractions;
using Newtonsoft.Json.Linq;

public sealed class NationalIdentifierGeneratorEditorFactory : IGeneratorConfigurationEditorFactory
{
    public string GeneratorType => NationalIdentifierGenerator.GeneratorType;

    public string GeneratorVersion => NationalIdentifierGenerator.GeneratorVersion;

    public IGeneratorConfigurationEditor Create(JObject options) => new NationalIdentifierGeneratorEditor(options);
}
