namespace Anonymyzer.Generators.Person;

using Anonymyzer.Base.Generation;
using Microsoft.Extensions.DependencyInjection;

public sealed class PersonGeneratorsLoader : IGeneratorsLoader
{
    public void RegisterGenerators(IServiceCollection serviceCollection)
    {
        serviceCollection.AddTransient<IGenerator, PersonIdentityGenerator>();
    }
}
