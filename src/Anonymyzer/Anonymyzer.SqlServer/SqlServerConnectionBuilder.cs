namespace Anonymyzer.SqlServer;

using System.Data;
using System.Data.SqlClient;
using Anonymyzer.Base;

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
        var conn =  new SqlConnection(connectionString);
        if (string.IsNullOrWhiteSpace(conn.Database))
        {
            conn.Open();
            conn.ChangeDatabase(dbName);
        }

        return conn;
    }
}