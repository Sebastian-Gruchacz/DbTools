namespace Anonymyzer.Base.Generation;

public interface IGenerator
{
    GeneratorDescriptor Descriptor { get; }

    IGeneratorConfigurationCodec Configuration { get; }

    IReadOnlyList<GeneratorDataRequirement> GetDataRequirements(
        GeneratorBinding binding,
        object configuration);

    ValueTask<IGeneratorSession> PrepareAsync(
        GeneratorPreparationContext context,
        object configuration,
        CancellationToken cancellationToken = default);
}
