namespace Anonymyzer.Console.Implementation;

using Anonymyzer.Base.Generation;
using Microsoft.Extensions.Logging;

internal class GeneratorsProvider : IGeneratorsProvider
{
    private readonly IEnumerable<IGenerator> _generators;
    private readonly ILogger<GeneratorsProvider> _logger;

    public GeneratorsProvider(IEnumerable<IGenerator> generators, ILogger<GeneratorsProvider> logger)
    {
        _generators = generators ?? throw new ArgumentNullException(nameof(generators));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }
}