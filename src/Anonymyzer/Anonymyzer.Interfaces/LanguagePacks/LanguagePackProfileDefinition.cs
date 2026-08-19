namespace Anonymyzer.Base.LanguagePacks;

using Newtonsoft.Json.Linq;

public sealed record LanguagePackProfileDefinition(
    string Id,
    string DisplayName,
    string GeneratorType,
    string GeneratorVersion,
    string Locale,
    JObject Options);
