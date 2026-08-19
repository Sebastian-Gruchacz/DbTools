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
            serviceCollection.AddTransient<IGenerator, JsonPathRedactorGenerator>();
            serviceCollection.AddTransient<IGenerator, SequentialTextGenerator>();
            serviceCollection.AddTransient<IGenerator, EmailAddressGenerator>();
            serviceCollection.AddTransient<IGenerator, PhoneNumberGenerator>();
            serviceCollection.AddTransient<IGenerator, UuidGenerator>();
            serviceCollection.AddTransient<IGenerator, TaxIdentifierGenerator>();
            serviceCollection.AddTransient<IGenerator, CompanyNameGenerator>();
            serviceCollection.AddTransient<IGenerator, AccountLoginGenerator>();
            serviceCollection.AddTransient<IGenerator, BankAccountGenerator>();
            serviceCollection.AddTransient<IGenerator, ReferencePseudonymGenerator>();
        }
    }
}
