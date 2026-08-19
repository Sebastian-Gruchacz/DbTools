namespace Anonymyzer.Generators.Simple.Wpf;

using Anonymyzer.ConfigEditor.Abstractions;
using Newtonsoft.Json.Linq;

public sealed class CompanyNameGeneratorEditorFactory : IGeneratorConfigurationEditorFactory
{
    public string GeneratorType => CompanyNameGenerator.GeneratorType;

    public string GeneratorVersion => CompanyNameGenerator.GeneratorVersion;

    public IGeneratorConfigurationEditor Create(JObject options) => new CompanyNameGeneratorEditor(options);
}
