namespace Anonymyzer.Generators.Simple.Wpf;

using Anonymyzer.ConfigEditor.Abstractions;
using Newtonsoft.Json.Linq;

public sealed class AccountLoginGeneratorEditorFactory : IGeneratorConfigurationEditorFactory
{
    public string GeneratorType => AccountLoginGenerator.GeneratorType;
    public string GeneratorVersion => AccountLoginGenerator.GeneratorVersion;
    public IGeneratorConfigurationEditor Create(JObject options) => new AccountLoginGeneratorEditor(options);
}
