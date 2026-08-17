namespace Anonymyzer.Base.Generation;

public abstract class GeneratorBase<TConfiguration> : IGenerator
    where TConfiguration : class
{
    public abstract GeneratorDescriptor Descriptor { get; }

    public abstract IGeneratorConfigurationCodec Configuration { get; }

    public IReadOnlyList<GeneratorDataRequirement> GetDataRequirements(
        GeneratorBinding binding,
        object configuration)
    {
        return GetDataRequirements(binding, RequireTyped(configuration));
    }

    public ValueTask<IGeneratorSession> PrepareAsync(
        GeneratorPreparationContext context,
        object configuration,
        CancellationToken cancellationToken = default)
    {
        return PrepareAsync(context, RequireTyped(configuration), cancellationToken);
    }

    protected abstract IReadOnlyList<GeneratorDataRequirement> GetDataRequirements(
        GeneratorBinding binding,
        TConfiguration configuration);

    protected abstract ValueTask<IGeneratorSession> PrepareAsync(
        GeneratorPreparationContext context,
        TConfiguration configuration,
        CancellationToken cancellationToken);

    private static TConfiguration RequireTyped(object configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        return configuration as TConfiguration
            ?? throw new ArgumentException(
                $"Expected configuration type {typeof(TConfiguration).FullName}, got {configuration.GetType().FullName}.",
                nameof(configuration));
    }
}
