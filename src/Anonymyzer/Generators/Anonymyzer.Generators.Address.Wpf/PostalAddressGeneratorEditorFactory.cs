namespace Anonymyzer.Generators.Address.Wpf;

using Anonymyzer.ConfigEditor.Abstractions;
using Newtonsoft.Json.Linq;

public sealed class PostalAddressGeneratorEditorFactory : IGeneratorConfigurationEditorFactory
{
    public string GeneratorType => PostalAddressGenerator.GeneratorType;

    public string GeneratorVersion => PostalAddressGenerator.GeneratorVersion;

    public IGeneratorConfigurationEditor Create(JObject options) => new PostalAddressGeneratorEditor(options);
}
