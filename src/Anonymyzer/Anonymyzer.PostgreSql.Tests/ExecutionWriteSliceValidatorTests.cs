namespace Anonymyzer.PostgreSql.Tests;

using Anonymyzer.Base;
using Anonymyzer.Base.Generation;
using Anonymyzer.Console.Planning;

public sealed class ExecutionWriteSliceValidatorTests
{
    [Fact]
    public void AcceptsSingleTableRowPlanWithUnchangedSinglePrimaryKey()
    {
        GeneratorExecutionPlanStep step = CreateStep(GeneratorExecutionScope.Row, "first_name");
        AnonymizationExecutionPlan plan = CreatePlan(step);
        ExecutionPlanDatabaseInspection inspection = CreateInspection(step, "id");

        ExecutionWriteSliceAssessment assessment = new ExecutionWriteSliceValidator().Assess(plan, inspection);

        Assert.True(assessment.IsSupported);
        Assert.Equal("id", assessment.PrimaryKeyColumn);
        Assert.Equal("people", assessment.TargetTable?.TableName);
        Assert.Contains(
            ExecutionPlanFormatter.Format(plan, inspection, assessment),
            line => line.Contains("Write slice ready", StringComparison.Ordinal));
    }

    [Fact]
    public void AcceptsSameTableColumnScopeWithCompleteScan()
    {
        var requirement = new GeneratorDataRequirement(
            "source-column",
            new GeneratorTableReference("public", "people"),
            ["notes"],
            GeneratorValueSource.Original,
            RequiresCompleteScan: true);
        GeneratorExecutionPlanStep step = CreateStep(
            GeneratorExecutionScope.Column,
            "notes",
            [requirement]);

        ExecutionWriteSliceAssessment assessment = new ExecutionWriteSliceValidator()
            .Assess(CreatePlan(step), CreateInspection(step, "id"));

        Assert.True(assessment.IsSupported);
        Assert.Contains("Row/Column", assessment.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RejectsRelationalScope()
    {
        GeneratorExecutionPlanStep step = CreateStep(GeneratorExecutionScope.Relational, "notes");

        ExecutionWriteSliceAssessment assessment = new ExecutionWriteSliceValidator()
            .Assess(CreatePlan(step), CreateInspection(step, "id"));

        Assert.False(assessment.IsSupported);
        Assert.Contains("unsupported scope Relational", assessment.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RejectsMissingOrCompositePrimaryKey()
    {
        GeneratorExecutionPlanStep step = CreateStep(GeneratorExecutionScope.Row, "first_name");
        var validator = new ExecutionWriteSliceValidator();

        ExecutionWriteSliceAssessment missing = validator.Assess(
            CreatePlan(step),
            CreateInspection(step));
        ExecutionWriteSliceAssessment composite = validator.Assess(
            CreatePlan(step),
            CreateInspection(step, "tenant_id", "id"));

        Assert.Contains("no primary key", missing.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("single-column primary key", composite.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RejectsPrimaryKeyConfiguredAsOutput()
    {
        GeneratorExecutionPlanStep step = CreateStep(GeneratorExecutionScope.Row, "id");

        ExecutionWriteSliceAssessment assessment = new ExecutionWriteSliceValidator()
            .Assess(CreatePlan(step), CreateInspection(step, "id"));

        Assert.False(assessment.IsSupported);
        Assert.Contains("primary-key column", assessment.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RejectsCrossTableRequirement()
    {
        var requirement = new GeneratorDataRequirement(
            "lookup",
            new GeneratorTableReference("public", "departments"),
            ["name"],
            GeneratorValueSource.Original,
            RequiresCompleteScan: false);
        GeneratorExecutionPlanStep step = CreateStep(
            GeneratorExecutionScope.Row,
            "first_name",
            [requirement]);

        ExecutionWriteSliceAssessment assessment = new ExecutionWriteSliceValidator()
            .Assess(CreatePlan(step), CreateInspection(step, "id"));

        Assert.False(assessment.IsSupported);
        Assert.Contains("reads another table", assessment.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RejectsCompleteScanOfGeneratedValues()
    {
        var requirement = new GeneratorDataRequirement(
            "generated-source",
            new GeneratorTableReference("public", "people"),
            ["name"],
            GeneratorValueSource.Generated,
            RequiresCompleteScan: true);
        GeneratorExecutionPlanStep step = CreateStep(
            GeneratorExecutionScope.Column,
            "notes",
            [requirement]);

        ExecutionWriteSliceAssessment assessment = new ExecutionWriteSliceValidator()
            .Assess(CreatePlan(step), CreateInspection(step, "id"));

        Assert.False(assessment.IsSupported);
        Assert.Contains("needs generated values", assessment.Message, StringComparison.Ordinal);
    }

    private static GeneratorExecutionPlanStep CreateStep(
        GeneratorExecutionScope scope,
        string outputColumn,
        IReadOnlyList<GeneratorDataRequirement>? requirements = null)
    {
        var table = new GeneratorTableReference("public", "people");
        var descriptor = new GeneratorDescriptor("Test", "1.0.0", "Test", scope, DbDataType.Text)
        {
            Outputs = [new GeneratorOutputDescriptor("Value", "Value", string.Empty, Required: true)]
        };
        var binding = new GeneratorBinding(
            table,
            new Dictionary<string, string> { ["Value"] = outputColumn });
        return new GeneratorExecutionPlanStep(
            "public.people/test",
            table,
            descriptor,
            binding,
            new object(),
            requirements ?? Array.Empty<GeneratorDataRequirement>(),
            1000);
    }

    private static AnonymizationExecutionPlan CreatePlan(params GeneratorExecutionPlanStep[] steps) =>
        new(1000, steps);

    private static ExecutionPlanDatabaseInspection CreateInspection(
        GeneratorExecutionPlanStep step,
        params string[] primaryKeyColumns) =>
        new(new Dictionary<string, GeneratorStepDatabaseInspection>
        {
            [step.Id] = new(
                25,
                primaryKeyColumns,
                new Dictionary<string, DataRequirementEstimate>())
        });
}
