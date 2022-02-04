using Anonymyzer.Console.GenerateConfiguration;
using Anonymyzer.Console.Processing;

using Microsoft.Extensions.DependencyInjection;

namespace Anonymyzer.Console;

internal static class LocalExtensions
{
    public static IServiceCollection AddCommands(this IServiceCollection services)
    {
        services.AddTransient<GenerateAnonymyzerConfigurationCommand>();
        services.AddTransient<ProcessAnonymyzerCommand>();

        return services;
    }
}