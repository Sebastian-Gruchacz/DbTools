namespace Anonymyzer.Base.Detection;

public interface IColumnCandidateRuleProvider
{
    IReadOnlyList<ColumnCandidateRule> GetRules();
}
