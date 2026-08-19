namespace Anonymyzer.Generators.Address;

using Anonymyzer.Base.Generation;

public sealed class PostalAddressGeneratorConfigurationCodec
    : GeneratorConfigurationCodec<PostalAddressGeneratorConfiguration>
{
    protected override PostalAddressGeneratorConfiguration CreateDefaultConfiguration() => new();

    protected override IEnumerable<string> ValidateConfiguration(PostalAddressGeneratorConfiguration configuration)
    {
        if (string.IsNullOrWhiteSpace(configuration.Locale))
        {
            yield return "Locale is required.";
        }
    }
}
