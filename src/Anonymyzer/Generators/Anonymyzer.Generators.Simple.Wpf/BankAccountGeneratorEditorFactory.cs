namespace Anonymyzer.Generators.Simple.Wpf;

using Anonymyzer.ConfigEditor.Abstractions;
using Newtonsoft.Json.Linq;

public sealed class BankAccountGeneratorEditorFactory : IGeneratorConfigurationEditorFactory
{
    public string GeneratorType => BankAccountGenerator.GeneratorType;

    public string GeneratorVersion => BankAccountGenerator.GeneratorVersion;

    public IGeneratorConfigurationEditor Create(JObject options) => new BankAccountGeneratorEditor(options);
}
