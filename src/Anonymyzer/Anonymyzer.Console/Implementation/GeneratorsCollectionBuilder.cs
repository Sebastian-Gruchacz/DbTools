namespace Anonymyzer.Console.Implementation;

using Anonymyzer.Base.Generation;

using Microsoft.Extensions.DependencyInjection;

internal class GeneratorsCollectionBuilder
{
    private readonly HashSet<Type> _loaders = new();

    public void Register(IServiceCollection serviceCollection)
    {
        // first register types, so can be instantiated using DI
        foreach (Type loaderType in _loaders)
        {
            serviceCollection.AddTransient(loaderType);
        }

        var buildServiceProvider = serviceCollection.BuildServiceProvider();

        foreach (Type loaderType in _loaders)
        {
            if (buildServiceProvider.GetService(loaderType) is IGeneratorsLoader loaderInstance)
            {
                loaderInstance.RegisterGenerators(serviceCollection);
            }
        }
    }

    public void AddLoader<TLoader>() where TLoader : IGeneratorsLoader
    {
       this.AddLoader(typeof(TLoader));
    }

    public void AddLoader(Type loaderType)
    {
        if (loaderType == null) throw new ArgumentNullException(nameof(loaderType));
        if (!typeof(IGeneratorsLoader).IsAssignableFrom(loaderType) || loaderType.IsAbstract || loaderType.IsInterface)
        {
            throw new ArgumentException($@"{loaderType.Name} must inherit from {nameof(IGeneratorsLoader)} and be instantiable type", nameof(loaderType));
        }

        this._loaders.Add(loaderType);
    }
}