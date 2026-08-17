using System.Data;

namespace Anonymyzer.Console.Implementation;

using Anonymyzer.Base;
using Anonymyzer.Console.Commands;
using Anonymyzer.Console.InternalInterfaces;
using Microsoft.Extensions.Logging;

public class DbConnectionFactory : IDbConnectionFactory
{
    private readonly IDbConnectionBuilder[] _builders;
    private readonly ILogger<DbConnectionFactory> _logger;

    public DbConnectionFactory(IEnumerable<IDbConnectionBuilder> builders, ILogger<DbConnectionFactory> logger)
    {
        _builders = builders.ToArray();
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public IDbConnection? CreateMainConnection(DbParameters config)
    {
        if (config == null) throw new ArgumentNullException(nameof(config));
        if (string.IsNullOrWhiteSpace(config.DatabaseEngine))
        {
            throw new ArgumentException($@"Database Engine name must be specified.", nameof(config));
        }

        var builder = _builders.SingleOrDefault(b =>
            config.DatabaseEngine.Equals(b.Name, StringComparison.InvariantCultureIgnoreCase));

        return builder?.BuildMainConnection(config.ConnectionString, config.DatabaseName);
    }

    public IDbConnection? CreateStructuralConnection(DbParameters config)
    {
        if (config == null) throw new ArgumentNullException(nameof(config));
        if (string.IsNullOrWhiteSpace(config.DatabaseEngine))
        {
            throw new ArgumentException($"Database Engine name must be specified.", nameof(config));
        }

        var builder = _builders.SingleOrDefault(b =>
            config.DatabaseEngine.Equals(b.Name, StringComparison.InvariantCultureIgnoreCase));

        return builder?.BuildStructuralConnection(config.StructuralConnectionString);
    }
}