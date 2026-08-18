namespace Anonymyzer.Generators.Simple;

using Anonymyzer.Base.Generation;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public sealed class JsonPathRedactorGeneratorConfigurationCodec
    : GeneratorConfigurationCodec<JsonPathRedactorGeneratorConfiguration>
{
    protected override JsonPathRedactorGeneratorConfiguration CreateDefaultConfiguration() => new()
    {
        Rules =
        [
            new JsonPathRedactionRuleConfiguration
            {
                Path = "$/sensitive",
                ReplacementJson = "\"REDACTED\""
            }
        ]
    };

    protected override IEnumerable<string> ValidateConfiguration(JsonPathRedactorGeneratorConfiguration configuration)
    {
        if (configuration.Rules is null || configuration.Rules.Count == 0)
        {
            yield return "At least one JSON redaction rule is required.";
            yield break;
        }

        var paths = new List<JsonRedactionPath>();
        for (int index = 0; index < configuration.Rules.Count; index++)
        {
            JsonPathRedactionRuleConfiguration? rule = configuration.Rules[index];
            if (rule is null)
            {
                yield return $"Rule {index + 1} cannot be null.";
                continue;
            }

            if (!JsonRedactionPath.TryParse(rule.Path, out JsonRedactionPath? path, out string pathError))
            {
                yield return $"Rule {index + 1}: {pathError}";
            }
            else
            {
                paths.Add(path!);
            }

            if (string.IsNullOrWhiteSpace(rule.ReplacementJson))
            {
                yield return $"Rule {index + 1}: replacement JSON is required.";
                continue;
            }

            if (!TryParseReplacement(rule.ReplacementJson, out string replacementError))
            {
                yield return $"Rule {index + 1}: replacement is not valid JSON ({replacementError}).";
            }
        }

        foreach (IGrouping<string, JsonRedactionPath> duplicate in paths.GroupBy(path => path.Value, StringComparer.Ordinal))
        {
            if (duplicate.Count() > 1)
            {
                yield return $"Path '{duplicate.Key}' is configured more than once.";
            }
        }

        for (int left = 0; left < paths.Count; left++)
        {
            for (int right = left + 1; right < paths.Count; right++)
            {
                if (paths[left].IsPrefixOf(paths[right]) || paths[right].IsPrefixOf(paths[left]))
                {
                    yield return $"Paths '{paths[left].Value}' and '{paths[right].Value}' overlap.";
                }
            }
        }
    }

    private static bool TryParseReplacement(string value, out string error)
    {
        try
        {
            JToken.Parse(value);
            error = string.Empty;
            return true;
        }
        catch (JsonReaderException exception)
        {
            error = exception.Message;
            return false;
        }
    }
}
