namespace Anonymyzer.Base;

using System.Data;

public interface IDbConnectionBuilder
{
    string Name { get; }

    IDbConnection BuildMainConnection(string connectionString, string dbName);

    IDbConnection BuildStructuralConnection(string connectionString);
}