namespace Anonymyzer.Base.Generation;

using System.Linq.Expressions;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public abstract class GeneratorBase<TConfig> : IGenerator
{
    private readonly JsonSerializer _serializer;

    protected GeneratorBase()
    {
        this._serializer = JsonSerializer.Create(new JsonSerializerSettings());
    }

    /// <inheritdoc cref="IGenerator"/>
    public Expression<Action<IRowNavigator>> BuildColumnWriter(IColumnInfo columnInfo, JObject globalConfig, JObject columnConfig)
    {
        // TODO: merge global and column config....

        TConfig config = globalConfig.ToObject<TConfig>();


        return BuildColumnWriter(columnInfo, config);
    }

    /// <inheritdoc cref="IGenerator"/>
    public JObject GetDefaultConfig()
    {
        var config = GetDefaultConfiguration();
        return JObject.FromObject(config);
    }

    /// <inheritdoc cref="IGenerator"/>
    public abstract string Name { get; }

    /// <inheritdoc cref="IGenerator"/>
    public abstract DbDataType SupportedDataType { get; }

    /// <inheritdoc cref="IGenerator"/>
    public abstract bool IsMatch(IColumnInfo columnInfo);

    /// <summary>
    /// Retrieves default configuration for this generator
    /// </summary>
    /// <returns></returns>
    public abstract TConfig GetDefaultConfiguration();

    /// <inheritdoc cref="IGenerator"/>
    protected abstract Expression<Action<IRowNavigator>> BuildColumnWriter(IColumnInfo columnInfo, TConfig config);



}