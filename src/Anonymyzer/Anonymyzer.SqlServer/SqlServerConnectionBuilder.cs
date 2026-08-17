namespace Anonymyzer.SqlServer;

using System.Data;
using Anonymyzer.Base;
using Microsoft.Data.SqlClient;

public class SqlServerConnectionBuilder : IDbConnectionBuilder
{
    public string Name { get; } = LibraryConstants.EngineName;

    public IDbConnection BuildStructuralConnection(string connectionString)
    {
        var conn = new SqlConnection(connectionString);

        return conn;
    }

    public IDbConnection BuildMainConnection(string connectionString, string dbName)
    {
        var conn = new SqlConnection(connectionString);
        if (string.IsNullOrWhiteSpace(conn.Database))
        {
            conn.Open();
            conn.ChangeDatabase(dbName);
        }

        return conn;
    }
}
