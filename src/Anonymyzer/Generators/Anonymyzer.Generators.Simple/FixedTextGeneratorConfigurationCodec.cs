namespace Anonymyzer.Generators.Simple;

using Anonymyzer.Base.Generation;

public sealed class FixedTextGeneratorConfigurationCodec
    : GeneratorConfigurationCodec<FixedTextGeneratorConfiguration>
{
    protected override FixedTextGeneratorConfiguration CreateDefaultConfiguration() => new();

    protected override IEnumerable<string> ValidateConfiguration(FixedTextGeneratorConfiguration configuration)
    {
        if (configuration.Value is null)
        {
            yield return "Value cannot be null. Use an empty string or PreserveNulls instead.";
        }
    }
}
