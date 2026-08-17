namespace Anonymyzer.Generators.Person.Wpf;

using Anonymyzer.ConfigEditor.Abstractions;
using Newtonsoft.Json.Linq;

public sealed class PersonIdentityGeneratorEditorFactory : IGeneratorConfigurationEditorFactory
{
    public string GeneratorType => PersonIdentityGenerator.GeneratorType;

    public string GeneratorVersion => PersonIdentityGenerator.GeneratorVersion;

    public IGeneratorConfigurationEditor Create(JObject options)
    {
        return new PersonIdentityGeneratorEditor(options);
    }
}
