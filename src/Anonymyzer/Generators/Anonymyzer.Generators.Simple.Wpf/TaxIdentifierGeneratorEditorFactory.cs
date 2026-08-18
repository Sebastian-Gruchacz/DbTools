namespace Anonymyzer.Generators.Simple.Wpf;

using Anonymyzer.ConfigEditor.Abstractions;
using Newtonsoft.Json.Linq;

public sealed class TaxIdentifierGeneratorEditorFactory : IGeneratorConfigurationEditorFactory
{
    public string GeneratorType => TaxIdentifierGenerator.GeneratorType;

    public string GeneratorVersion => TaxIdentifierGenerator.GeneratorVersion;

    public IGeneratorConfigurationEditor Create(JObject options) => new TaxIdentifierGeneratorEditor(options);
}
