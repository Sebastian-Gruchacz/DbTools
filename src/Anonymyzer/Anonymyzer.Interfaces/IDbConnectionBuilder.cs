namespace Anonymyzer.Base;

using System.Data;

public interface IDbConnectionBuilder
{
    string Name { get; }

    IDbConnection BuildConnection(string connectionString);
}