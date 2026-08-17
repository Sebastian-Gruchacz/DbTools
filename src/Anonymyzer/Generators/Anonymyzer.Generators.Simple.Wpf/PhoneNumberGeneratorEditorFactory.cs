namespace Anonymyzer.Generators.Simple.Wpf;

using Anonymyzer.ConfigEditor.Abstractions;
using Newtonsoft.Json.Linq;

public sealed class PhoneNumberGeneratorEditorFactory : IGeneratorConfigurationEditorFactory
{
    public string GeneratorType => PhoneNumberGenerator.GeneratorType;

    public string GeneratorVersion => PhoneNumberGenerator.GeneratorVersion;

    public IGeneratorConfigurationEditor Create(JObject options) => new PhoneNumberGeneratorEditor(options);
}
