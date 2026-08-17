namespace Anonymyzer.LanguagePack.Polish;

using Anonymyzer.Base.Detection;
using Anonymyzer.Generators.Person;
using Microsoft.Extensions.DependencyInjection;

public static class PolishLanguagePackLoader
{
    public static IServiceCollection AddPolishLanguagePack(this IServiceCollection services)
    {
        services.AddSingleton<IPersonLocaleDataProvider, PolishPersonLocaleDataProvider>();
        services.AddSingleton<IColumnCandidateRuleProvider, PolishColumnCandidateRuleProvider>();
        return services;
    }
}
