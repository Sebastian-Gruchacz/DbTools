namespace Anonymyzer.Base.Generation;

public interface IGeneratorSession : IAsyncDisposable
{
    ValueTask ApplyAsync(IGeneratorRow row, CancellationToken cancellationToken = default);
}
