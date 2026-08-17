namespace Anonymyzer.Base.Detection;

public sealed record ColumnCandidateRule(
    string Id,
    string Locale,
    string SemanticRole,
    string NamePattern,
    decimal Confidence);
