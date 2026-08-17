namespace Anonymyzer.Base.Generation;

using Newtonsoft.Json.Linq;

public interface IGeneratorConfigurationCodec
{
    Type ConfigurationType { get; }

    object CreateDefault();

    object Deserialize(JObject json);

    JObject Serialize(object configuration);

    IReadOnlyList<string> Validate(object configuration);
}
