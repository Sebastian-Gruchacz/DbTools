namespace Anonymyzer.Configuration;

/// <summary>
/// Records why a column was proposed for anonymization. A proposal is never approval.
/// </summary>
public sealed class CandidateDetectionConfiguration
{
    public bool IsCandidate { get; set; }

    public string SuggestedRole { get; set; } = string.Empty;

    public string Locale { get; set; } = string.Empty;

    public decimal Confidence { get; set; }

    public string MatchedRule { get; set; } = string.Empty;
}
