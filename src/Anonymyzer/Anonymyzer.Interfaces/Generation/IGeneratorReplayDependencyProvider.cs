namespace Anonymyzer.Base.Generation;

public interface IGeneratorReplayDependencyProvider
{
    IReadOnlyList<string> GetReplayEnvironmentVariables(object configuration);
}
