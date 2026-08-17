namespace Anonymyzer.PostgreSql;

using System.Data;
using Anonymyzer.Base;

public sealed class PostgreSqlEngineBuilder : IAnonymyzerEngineBuilder
{
    public string Name => LibraryConstants.EngineName;

    public IAnonymyzerEngine BuildEngine(IDbConnection connection) =>
        new PostgreSqlAnonymyzerEngine(connection);
}
