namespace Anonymyzer.Console.InternalInterfaces;

using System.Data;
using Anonymyzer.Console.Commands;

public interface IDbConnectionFactory
{
    IDbConnection? CreateMainConnection(DbParameters config);

    IDbConnection? CreateStructuralConnection(DbParameters config);
}