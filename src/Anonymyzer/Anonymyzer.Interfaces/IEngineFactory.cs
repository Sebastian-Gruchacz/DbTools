namespace Anonymyzer.Base;

using System.Data;

public interface IEngineFactory
{
    IAnonymyzerEngine? CreateEngine(string engineName, IDbConnection connection);
}