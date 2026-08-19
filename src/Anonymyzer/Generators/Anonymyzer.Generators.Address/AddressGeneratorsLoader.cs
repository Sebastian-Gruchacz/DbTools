namespace Anonymyzer.Generators.Address;

using Anonymyzer.Base.Generation;
using Microsoft.Extensions.DependencyInjection;

public sealed class AddressGeneratorsLoader : IGeneratorsLoader
{
    public void RegisterGenerators(IServiceCollection serviceCollection)
    {
        serviceCollection.AddTransient<IGenerator, PostalAddressGenerator>();
    }
}
