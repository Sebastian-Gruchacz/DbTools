namespace Anonymyzer.Generators.Simple
{
    using Anonymyzer.Base.Generation;
    
    using Microsoft.Extensions.DependencyInjection;
    
    public class SimpleGeneratorsLoader : IGeneratorsLoader
    {
        public void RegisterGenerators(IServiceCollection serviceCollection)
        {
            serviceCollection.AddTransient<IGenerator, ShufflingTextGenerator>();
            serviceCollection.AddTransient<IGenerator, FixedTextGenerator>();
            serviceCollection.AddTransient<IGenerator, SequentialTextGenerator>();
            serviceCollection.AddTransient<IGenerator, EmailAddressGenerator>();
        }
    }
}
