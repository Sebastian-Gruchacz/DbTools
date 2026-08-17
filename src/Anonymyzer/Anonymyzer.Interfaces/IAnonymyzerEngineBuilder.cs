namespace Anonymyzer.Base;

using System.Data;

public interface IAnonymyzerEngineBuilder
{
    string Name { get; }

    IAnonymyzerEngine BuildEngine(IDbConnection connection);
}