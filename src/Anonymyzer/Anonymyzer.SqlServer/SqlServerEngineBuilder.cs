namespace Anonymyzer.SqlServer;

using System.Data;
using Anonymyzer.Base;

public class SqlServerEngineBuilder : IAnonymyzerEngineBuilder
{
    public string Name { get; } = LibraryConstants.EngineName;
    public IAnonymyzerEngine BuildEngine(IDbConnection connection)
    {
        return new SqlServerAnonymyzerEngine(connection);
    }
}