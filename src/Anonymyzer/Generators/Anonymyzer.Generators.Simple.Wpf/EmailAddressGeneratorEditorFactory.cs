namespace Anonymyzer.Generators.Simple.Wpf;

using Anonymyzer.ConfigEditor.Abstractions;
using Newtonsoft.Json.Linq;

public sealed class EmailAddressGeneratorEditorFactory : IGeneratorConfigurationEditorFactory
{
    public string GeneratorType => EmailAddressGenerator.GeneratorType;

    public string GeneratorVersion => EmailAddressGenerator.GeneratorVersion;

    public IGeneratorConfigurationEditor Create(JObject options) => new EmailAddressGeneratorEditor(options);
}
