namespace Anonymyzer.PostgreSql.Tests;

using Anonymyzer.Base.Generation;
using Anonymyzer.Console.Planning;
using Anonymyzer.Generators.Simple;

public sealed class ReplayDependencyFingerprintTests
{
    private const string CheckpointSecret = "test-checkpoint-secret-with-sufficient-entropy";

    [Fact]
    public void BindsEnvironmentSecretWithoutReturningIt()
    {
        string variableName = $"ANONYMYZER_REPLAY_TEST_{Guid.NewGuid():N}";
        const string firstSecret = "first-pseudonym-secret-with-sufficient-entropy";
        const string secondSecret = "second-pseudonym-secret-with-sufficient-entropy";
        var generator = new ReferencePseudonymGenerator();
        var table = new GeneratorTableReference("public", "people");
        var configuration = new ReferencePseudonymGeneratorConfiguration
        {
            ReferenceColumn = "department_id",
            LookupSchema = "public",
            LookupTable = "departments",
            LookupKeyColumn = "id",
            KeyEnvironmentVariable = variableName
        };
        var binding = new GeneratorBinding(
            table,
            new Dictionary<string, string> { [ReferencePseudonymGenerator.ValueOutput] = "alias" });
        var plan = new AnonymizationExecutionPlan(
            100,
            [new GeneratorExecutionPlanStep(
                "public.people/alias",
                table,
                generator.Descriptor,
                binding,
                configuration,
                generator.GetDataRequirements(binding, configuration),
                100)]);

        try
        {
            Environment.SetEnvironmentVariable(variableName, firstSecret);
            IReadOnlyDictionary<string, string> first = ReplayDependencyFingerprint.Compute(
                plan,
                [generator],
                CheckpointSecret);
            Environment.SetEnvironmentVariable(variableName, secondSecret);
            IReadOnlyDictionary<string, string> second = ReplayDependencyFingerprint.Compute(
                plan,
                [generator],
                CheckpointSecret);

            Assert.Single(first);
            Assert.False(ReplayDependencyFingerprint.Matches(first, second));
            Assert.DoesNotContain(firstSecret, string.Join(string.Empty, first.Values), StringComparison.Ordinal);
            Assert.DoesNotContain(secondSecret, string.Join(string.Empty, second.Values), StringComparison.Ordinal);
        }
        finally
        {
            Environment.SetEnvironmentVariable(variableName, null);
        }
    }
}
