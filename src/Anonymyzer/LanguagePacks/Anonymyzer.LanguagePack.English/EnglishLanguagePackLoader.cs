namespace Anonymyzer.LanguagePack.English;

using Anonymyzer.Base.Detection;
using Anonymyzer.Base.LanguagePacks;
using Anonymyzer.Generators.Address;
using Anonymyzer.Generators.Person;
using Anonymyzer.Generators.Simple;
using Microsoft.Extensions.DependencyInjection;

public static class EnglishLanguagePackLoader
{
    public static IServiceCollection AddEnglishLanguagePack(this IServiceCollection services)
    {
        services.AddSingleton<ILanguagePack, EnglishLanguagePack>();
        services.AddSingleton<IColumnCandidateRuleProvider, EnglishColumnCandidateRuleProvider>();
        services.AddSingleton<IPostalAddressLocaleDataProvider, EnglishPostalAddressLocaleDataProvider>();
        services.AddSingleton<IPersonLocaleDataProvider, EnglishPersonLocaleDataProvider>();
        services.AddSingleton<IPhoneNumberLocaleDataProvider, EnglishPhoneNumberLocaleDataProvider>();
        services.AddSingleton<INationalIdentifierLocaleDataProvider, EnglishNationalIdentifierLocaleDataProvider>();
        services.AddSingleton<ICompanyNameLocaleDataProvider, EnglishCompanyNameLocaleDataProvider>();
        return services;
    }
}
