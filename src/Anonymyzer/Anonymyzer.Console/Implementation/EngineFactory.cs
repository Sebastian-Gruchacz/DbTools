using System.Data;

namespace Anonymyzer.Console.Implementation;

using Anonymyzer.Base;
using Microsoft.Extensions.Logging;

public class EngineFactory : IEngineFactory
{
    private readonly IAnonymyzerEngineBuilder[] _builders;
    private readonly ILogger<EngineFactory> _logger;

    public EngineFactory(IEnumerable<IAnonymyzerEngineBuilder> configuredBuilders, ILogger<EngineFactory> logger)
    {
        _builders = configuredBuilders.ToArray();
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public IAnonymyzerEngine? CreateEngine(string engineName, IDbConnection connection)
    {
        if (string.IsNullOrWhiteSpace(engineName))
        {
            throw new ArgumentException("Value cannot be null or whitespace.", nameof(engineName));
        }

        var builder = _builders.SingleOrDefault(b =>
            engineName.Equals(b.Name, StringComparison.InvariantCultureIgnoreCase));

        return builder?.BuildEngine(connection);
    }
}