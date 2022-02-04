namespace Anonymyzer.SqlServer;

using System.Data;
using System.Data.SqlClient;
using Anonymyzer.Base;

public class SqlServerConnectionBuilder : IDbConnectionBuilder
{
    public string Name { get; } = LibraryConstants.EngineName;

    public IDbConnection BuildConnection(string connectionString)
    {
        return new SqlConnection(connectionString);
    }
}