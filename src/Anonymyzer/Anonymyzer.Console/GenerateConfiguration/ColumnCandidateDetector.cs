namespace Anonymyzer.Console.GenerateConfiguration;

using System.Globalization;
using System.Text;
using Anonymyzer.Base.Detection;
using Anonymyzer.Configuration;

internal sealed class ColumnCandidateDetector
{
    private static readonly HashSet<string> NonValueTokens = new(StringComparer.Ordinal)
    {
        "active", "allowed", "enabled", "flag", "is", "required", "status", "type", "verified"
    };

    private readonly IReadOnlyList<PreparedRule> _rules;

    public ColumnCandidateDetector(IEnumerable<IColumnCandidateRuleProvider> providers)
    {
        ArgumentNullException.ThrowIfNull(providers);
        _rules = providers
            .SelectMany(provider => provider.GetRules())
            .Select(rule => new PreparedRule(rule, Tokenize(rule.NamePattern)))
            .Where(rule => rule.Tokens.Count > 0)
            .OrderBy(rule => rule.Rule.Id, StringComparer.Ordinal)
            .ToArray();
    }

    public CandidateDetectionConfiguration Detect(string columnName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(columnName);

        IReadOnlyList<string> columnTokens = Tokenize(columnName);
        Match? bestMatch = _rules
            .Select(rule => TryMatch(rule, columnTokens))
            .Where(match => match is not null)
            .Cast<Match>()
            .OrderByDescending(match => match.Confidence)
            .ThenByDescending(match => match.Rule.Tokens.Count)
            .ThenBy(match => match.Rule.Rule.Id, StringComparer.Ordinal)
            .FirstOrDefault();

        return bestMatch is null
            ? new CandidateDetectionConfiguration()
            : new CandidateDetectionConfiguration
            {
                IsCandidate = true,
                SuggestedRole = bestMatch.Rule.Rule.SemanticRole,
                Locale = bestMatch.Rule.Rule.Locale,
                Confidence = bestMatch.Confidence,
                MatchedRule = bestMatch.Rule.Rule.Id
            };
    }

    private static Match? TryMatch(PreparedRule rule, IReadOnlyList<string> columnTokens)
    {
        if (columnTokens.Count < rule.Tokens.Count)
        {
            return null;
        }

        for (int start = 0; start <= columnTokens.Count - rule.Tokens.Count; start++)
        {
            if (!rule.Tokens.SequenceEqual(columnTokens.Skip(start).Take(rule.Tokens.Count)))
            {
                continue;
            }

            IEnumerable<string> remainingTokens = columnTokens.Take(start)
                .Concat(columnTokens.Skip(start + rule.Tokens.Count));
            if (remainingTokens.Any(NonValueTokens.Contains))
            {
                return null;
            }

            decimal confidence = columnTokens.Count == rule.Tokens.Count
                ? rule.Rule.Confidence
                : Math.Max(0m, rule.Rule.Confidence - 0.05m);
            return new Match(rule, confidence);
        }

        return null;
    }

    internal static IReadOnlyList<string> Tokenize(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Array.Empty<string>();
        }

        string decomposed = name.Normalize(NormalizationForm.FormD);
        var normalized = new StringBuilder(decomposed.Length * 2);
        char previous = '\0';
        for (int index = 0; index < decomposed.Length; index++)
        {
            char character = decomposed[index];
            UnicodeCategory category = CharUnicodeInfo.GetUnicodeCategory(character);
            if (category == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (!char.IsLetterOrDigit(character))
            {
                normalized.Append(' ');
                previous = character;
                continue;
            }

            bool startsWord = char.IsUpper(character)
                && (char.IsLower(previous)
                    || char.IsUpper(previous)
                    && index + 1 < decomposed.Length
                    && char.IsLower(decomposed[index + 1]));
            if (startsWord)
            {
                normalized.Append(' ');
            }

            normalized.Append(char.ToLowerInvariant(character));
            previous = character;
        }

        return normalized.ToString()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private sealed record PreparedRule(ColumnCandidateRule Rule, IReadOnlyList<string> Tokens);

    private sealed record Match(PreparedRule Rule, decimal Confidence);
}
