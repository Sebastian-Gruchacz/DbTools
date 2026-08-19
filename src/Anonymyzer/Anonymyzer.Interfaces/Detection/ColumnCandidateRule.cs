namespace Anonymyzer.Base.Detection;

public sealed record ColumnCandidateRule(
    string Id,
    string Locale,
    string SemanticRole,
    string NamePattern,
    decimal Confidence)
{
    public IReadOnlySet<string> ExcludedTokens { get; init; } = new HashSet<string>(StringComparer.Ordinal);
}
