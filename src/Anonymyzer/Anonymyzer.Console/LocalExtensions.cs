using Anonymyzer.Console.GenerateConfiguration;
using Microsoft.Extensions.DependencyInjection;

namespace Anonymyzer.Console;

using Anonymyzer.Base;
using Anonymyzer.Console.Commands;
using Anonymyzer.Console.Implementation;
using Anonymyzer.Console.InternalInterfaces;
using Anonymyzer.Console.Safety;
using Anonymyzer.Generators.Person;
using Anonymyzer.Generators.Simple;
using Anonymyzer.LanguagePack.English;
using Anonymyzer.LanguagePack.Polish;
using Anonymyzer.PostgreSql;
using Anonymyzer.SqlServer;
using Microsoft.Extensions.DependencyInjection.Extensions;

internal static class LocalExtensions
{
    public static IServiceCollection AddDatabaseEngines(this IServiceCollection services)
    {
        services.AddTransient<IAnonymyzerEngineBuilder, SqlServerEngineBuilder>();
        services.AddTransient<IDbConnectionBuilder, SqlServerConnectionBuilder>();
        services.AddTransient<IAnonymyzerEngineBuilder, PostgreSqlEngineBuilder>();
        services.AddTransient<IDbConnectionBuilder, PostgreSqlConnectionBuilder>();

        return services;
    }

    public static IServiceCollection AddCommands(this IServiceCollection services)
    {
        services.AddSingleton<IDetachedCopyMarkerReader, DetachedCopyMarkerReader>();
        services.AddSingleton<DetachedCopySafetyValidator>();
        services.AddSingleton<ColumnCandidateDetector>();
        services.AddTransient<GenerateAnonymyzerConfigurationCommand>();
        services.AddTransient<ProcessAnonymyzerCommand>();

        return services;
    }

    public static IServiceCollection AddBuiltInGenerators(this IServiceCollection services)
    {
        services.AddEnglishLanguagePack();
        services.AddPolishLanguagePack();
        return services.AddGenerators(builder =>
        {
            builder.AddLoader<SimpleGeneratorsLoader>();
            builder.AddLoader<PersonGeneratorsLoader>();
        });
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
