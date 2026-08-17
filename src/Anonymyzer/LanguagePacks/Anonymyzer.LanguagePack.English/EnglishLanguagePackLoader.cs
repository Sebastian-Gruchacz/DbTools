namespace Anonymyzer.LanguagePack.English;

using Anonymyzer.Base.Detection;
using Microsoft.Extensions.DependencyInjection;

public static class EnglishLanguagePackLoader
{
    public static IServiceCollection AddEnglishLanguagePack(this IServiceCollection services)
    {
        services.AddSingleton<IColumnCandidateRuleProvider, EnglishColumnCandidateRuleProvider>();
        return services;
    }
}
