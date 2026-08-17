namespace Anonymyzer.Console.CommandLibraryElements;

using Microsoft.Extensions.Logging;

public class CommandLogger : ICommandLogger
{
    private readonly ILogger<CommandLogger> _logger;

    public CommandLogger(ILogger<CommandLogger> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public void Error(string message)
    {
        _logger.LogError(message);
    }

    public void Warning(string message)
    {
        _logger.LogWarning(message);
    }
}