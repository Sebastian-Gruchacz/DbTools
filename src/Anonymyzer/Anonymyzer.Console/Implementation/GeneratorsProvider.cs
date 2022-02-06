namespace Anonymyzer.Console.Implementation;

using Anonymyzer.Base.Generation;
using Anonymyzer.Console.InternalInterfaces;

using Microsoft.Extensions.Logging;

internal class GeneratorsProvider : IGeneratorsProvider
{
    private readonly IGenerator[] _generators;
    private readonly ILogger<GeneratorsProvider> _logger;

    public GeneratorsProvider(IEnumerable<IGenerator> generators, ILogger<GeneratorsProvider> logger)
    {
        if (generators == null) throw new ArgumentNullException(nameof(generators));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _generators = generators.ToArray();
    }

    public IEnumerable<IGenerator> GetAllGenerators()
    {
        return _generators;
    }
}