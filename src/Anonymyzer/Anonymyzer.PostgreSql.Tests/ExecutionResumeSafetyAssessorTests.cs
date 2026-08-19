namespace Anonymyzer.PostgreSql.Tests;

using Anonymyzer.Base;
using Anonymyzer.Base.Generation;
using Anonymyzer.Console.Planning;

public sealed class ExecutionResumeSafetyAssessorTests
{
    [Fact]
    public void AcceptsRowPlanThatReadsOnlyUnchangedOriginalColumns()
    {
        GeneratorExecutionPlanStep step = CreateStep(
            GeneratorExecutionScope.Row,
            requiresExistingValue: false,
            outputColumn: "email",
            [new GeneratorDataRequirement(
                "name",
                new GeneratorTableReference("public", "people"),
                ["first_name", "last_name"],
                GeneratorValueSource.Original,
                RequiresCompleteScan: false)]);

        ExecutionResumeSafetyAssessment result = new ExecutionResumeSafetyAssessor()
            .Assess(new AnonymizationExecutionPlan(100, [step]));

        Assert.True(result.IsSupported);
    }

    [Theory]
    [InlineData(GeneratorExecutionScope.Column, false, "scope Column")]
    [InlineData(GeneratorExecutionScope.Row, true, "value it overwrites")]
    public void RejectsStateThatCannotBeReconstructed(
        GeneratorExecutionScope scope,
        bool requiresExistingValue,
        string expectedReason)
    {
        GeneratorExecutionPlanStep step = CreateStep(
            scope,
            requiresExistingValue,
            outputColumn: "value");

        ExecutionResumeSafetyAssessment result = new ExecutionResumeSafetyAssessor()
            .Assess(new AnonymizationExecutionPlan(100, [step]));

        Assert.False(result.IsSupported);
        Assert.Contains(expectedReason, result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AcceptsRelationalPlanWithReadOnlyExternalCompleteScan()
    {
        var target = new GeneratorTableReference("public", "people");
        var lookup = new GeneratorTableReference("public", "departments");
        GeneratorExecutionPlanStep step = CreateStep(
            GeneratorExecutionScope.Relational,
            requiresExistingValue: false,
            outputColumn: "alias",
            [
                new GeneratorDataRequirement(
                    "target-reference",
                    target,
                    ["department_id"],
                    GeneratorValueSource.Original,
                    RequiresCompleteScan: false),
                new GeneratorDataRequirement(
                    "lookup-keys",
                    lookup,
                    ["alias"],
                    GeneratorValueSource.Original,
                    RequiresCompleteScan: true)
            ]);

        ExecutionResumeSafetyAssessment result = new ExecutionResumeSafetyAssessor()
            .Assess(new AnonymizationExecutionPlan(100, [step]));

        Assert.True(result.IsSupported);
        Assert.Contains("Relational", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RejectsCompleteScanOfTargetTable()
    {
        GeneratorExecutionPlanStep step = CreateStep(
            GeneratorExecutionScope.Relational,
            requiresExistingValue: false,
            outputColumn: "alias",
            [new GeneratorDataRequirement(
                "target-scan",
                new GeneratorTableReference("public", "people"),
                ["department_id"],
                GeneratorValueSource.Original,
                RequiresCompleteScan: true)]);

        ExecutionResumeSafetyAssessment result = new ExecutionResumeSafetyAssessor()
            .Assess(new AnonymizationExecutionPlan(100, [step]));

        Assert.False(result.IsSupported);
        Assert.Contains("mutable target", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RejectsOriginalInputThatWasAlreadyOverwritten()
    {
        GeneratorExecutionPlanStep step = CreateStep(
            GeneratorExecutionScope.Row,
            requiresExistingValue: false,
            outputColumn: "name",
            [new GeneratorDataRequirement(
                "source-name",
                new GeneratorTableReference("public", "people"),
                ["name"],
                GeneratorValueSource.Original,
                RequiresCompleteScan: false)]);

        ExecutionResumeSafetyAssessment result = new ExecutionResumeSafetyAssessor()
            .Assess(new AnonymizationExecutionPlan(100, [step]));

        Assert.False(result.IsSupported);
        Assert.Contains("overwritten", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static GeneratorExecutionPlanStep CreateStep(
        GeneratorExecutionScope scope,
        bool requiresExistingValue,
        string outputColumn,
        IReadOnlyList<GeneratorDataRequirement>? requirements = null)
    {
        var table = new GeneratorTableReference("public", "people");
        var descriptor = new GeneratorDescriptor("Test", "1.0.0", "Test", scope, DbDataType.Text)
        {
            RequiresExistingValue = requiresExistingValue,
            SupportsDeterministicReplay = true
        };
        return new GeneratorExecutionPlanStep(
            "public.people/test",
            table,
            descriptor,
            new GeneratorBinding(table, new Dictionary<string, string> { ["Value"] = outputColumn }),
            new object(),
            requirements ?? Array.Empty<GeneratorDataRequirement>(),
            100);
    }
}
