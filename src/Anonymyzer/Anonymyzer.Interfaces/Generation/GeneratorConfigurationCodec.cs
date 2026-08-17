namespace Anonymyzer.Base.Generation;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public abstract class GeneratorConfigurationCodec<TConfiguration> : IGeneratorConfigurationCodec
    where TConfiguration : class
{
    private readonly JsonSerializer _serializer = JsonSerializer.CreateDefault();

    public Type ConfigurationType => typeof(TConfiguration);

    public object CreateDefault() => CreateDefaultConfiguration();

    public object Deserialize(JObject json)
    {
        ArgumentNullException.ThrowIfNull(json);
        return json.ToObject<TConfiguration>(_serializer)
            ?? throw new JsonSerializationException($"Could not deserialize {typeof(TConfiguration).Name} configuration.");
    }

    public JObject Serialize(object configuration)
    {
        return JObject.FromObject(RequireTyped(configuration), _serializer);
    }

    public IReadOnlyList<string> Validate(object configuration)
    {
        return ValidateConfiguration(RequireTyped(configuration)).ToArray();
    }

    protected abstract TConfiguration CreateDefaultConfiguration();

    protected virtual IEnumerable<string> ValidateConfiguration(TConfiguration configuration)
    {
        yield break;
    }

    private static TConfiguration RequireTyped(object configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        return configuration as TConfiguration
            ?? throw new ArgumentException(
                $"Expected configuration type {typeof(TConfiguration).FullName}, got {configuration.GetType().FullName}.",
                nameof(configuration));
    }
}
