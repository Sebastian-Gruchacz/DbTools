namespace Anonymyzer.Base.Generation;

public interface IGeneratorDataReader
{
    IAsyncEnumerable<GeneratorDataRow> ReadAsync(
        GeneratorDataRequirement requirement,
        CancellationToken cancellationToken = default);
}
