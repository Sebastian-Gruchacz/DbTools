using Anonymyzer.Console.GenerateConfiguration;
using Anonymyzer.Console.Processing;

using Microsoft.Extensions.DependencyInjection;

namespace Anonymyzer.Console;

using Anonymyzer.Console.Implementation;

internal static class LocalExtensions
{
    public static IServiceCollection AddCommands(this IServiceCollection services)
    {
        services.AddTransient<GenerateAnonymyzerConfigurationCommand>();
        services.AddTransient<ProcessAnonymyzerCommand>();

        return services;
    }

    public static IServiceCollection AddGenerators(this IServiceCollection services, Action<GeneratorsCollectionBuilder> callbackAction)
    {
        var builder = new GeneratorsCollectionBuilder();
        callbackAction.Invoke(builder);

        builder.Register(services);

        services.AddSingleton<IGeneratorsProvider, GeneratorsProvider>();

        return services;
    }
}