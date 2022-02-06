namespace Anonymyzer.Generators.Simple
{
    using Anonymyzer.Base.Generation;
    
    using Microsoft.Extensions.DependencyInjection;
    
    public class SimpleGeneratorsLoader : IGeneratorsLoader
    {
        public void RegisterGenerators(IServiceCollection serviceCollection)
        {
            // TODO: manual or reflection - other types in assembly

            serviceCollection.AddTransient<ShufflingTextGenerator>();



        }
    }
}
