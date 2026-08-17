using Anonymyzer.Console.GenerateConfiguration;
using Microsoft.Extensions.DependencyInjection;

namespace Anonymyzer.Console;

using Anonymyzer.Console.Commands;
using Anonymyzer.Console.Implementation;
using Anonymyzer.Console.InternalInterfaces;
using Microsoft.Extensions.DependencyInjection.Extensions;

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

        services.TryAddSingleton(typeof(IGeneratorsProvider), typeof(GeneratorsProvider));
        //services.AddSingleton<IGeneratorsProvider, GeneratorsProvider>();

        return services;
    }
}