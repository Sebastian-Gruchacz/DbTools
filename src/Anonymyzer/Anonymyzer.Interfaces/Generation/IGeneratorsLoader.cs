namespace Anonymyzer.Base.Generation;

using Microsoft.Extensions.DependencyInjection;

public interface IGeneratorsLoader
{
    void RegisterGenerators(IServiceCollection serviceCollection);
}