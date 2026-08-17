namespace Anonymyzer.Base.Generation;

public sealed class GeneratorPreparationContext
{
    public GeneratorPreparationContext(GeneratorBinding binding, IGeneratorDataReader dataReader)
    {
        Binding = binding ?? throw new ArgumentNullException(nameof(binding));
        DataReader = dataReader ?? throw new ArgumentNullException(nameof(dataReader));
    }

    public GeneratorBinding Binding { get; }

    public IGeneratorDataReader DataReader { get; }
}
