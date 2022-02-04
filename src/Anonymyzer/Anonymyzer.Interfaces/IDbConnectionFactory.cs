namespace Anonymyzer.Base;

using System.Data;

public interface IDbConnectionFactory
{
    IDbConnection? CreateConnection(string engineName, string connectionString);
}