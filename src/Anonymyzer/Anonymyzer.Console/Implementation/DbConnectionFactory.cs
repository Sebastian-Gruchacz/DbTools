using System.Data;

namespace Anonymyzer.Console.Implementation;

using Anonymyzer.Base;
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

    public IDbConnection? CreateConnection(string engineName, string connectionString)
    {
        if (string.IsNullOrWhiteSpace(engineName))
        {
            throw new ArgumentException("Value cannot be null or whitespace.", nameof(engineName));
        }

        var builder = _builders.SingleOrDefault(b => 
                engineName.Equals(b.Name, StringComparison.InvariantCultureIgnoreCase));

        return builder?.BuildConnection(connectionString);
    }
}