namespace Anonymyzer.PostgreSql;

using System.Data;
using Anonymyzer.Base;
using Npgsql;

public sealed class PostgreSqlConnectionBuilder : IDbConnectionBuilder
{
    public string Name => LibraryConstants.EngineName;

    public IDbConnection BuildMainConnection(string connectionString, string dbName)
    {
        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        if (string.IsNullOrWhiteSpace(builder.Database))
        {
            builder.Database = dbName;
        }

        return new NpgsqlConnection(builder.ConnectionString);
    }

    public IDbConnection BuildStructuralConnection(string connectionString) =>
        new NpgsqlConnection(connectionString);
}
