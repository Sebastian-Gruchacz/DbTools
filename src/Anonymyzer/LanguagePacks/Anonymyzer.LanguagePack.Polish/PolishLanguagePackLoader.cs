namespace Anonymyzer.LanguagePack.Polish;

using Anonymyzer.Base.Detection;
using Anonymyzer.Generators.Person;
using Anonymyzer.Generators.Simple;
using Microsoft.Extensions.DependencyInjection;

public static class PolishLanguagePackLoader
{
    public static IServiceCollection AddPolishLanguagePack(this IServiceCollection services)
    {
        services.AddSingleton<IPersonLocaleDataProvider, PolishPersonLocaleDataProvider>();
        services.AddSingleton<IPhoneNumberLocaleDataProvider, PolishPhoneNumberLocaleDataProvider>();
        services.AddSingleton<ITaxIdentifierLocaleDataProvider, PolishTaxIdentifierLocaleDataProvider>();
        services.AddSingleton<INationalIdentifierLocaleDataProvider, PolishNationalIdentifierLocaleDataProvider>();
        services.AddSingleton<IColumnCandidateRuleProvider, PolishColumnCandidateRuleProvider>();
        return services;
    }
}
