namespace Anonymyzer.Console.Planning;

using Anonymyzer.Base.Generation;

internal static class ReplayDependencyFingerprint
{
    public static IReadOnlyDictionary<string, string> Compute(
        AnonymizationExecutionPlan plan,
        IEnumerable<IGenerator> generators,
        string checkpointSecret)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(generators);
        PrimaryKeyFingerprint.EnsureSecretIsValid(checkpointSecret);

        IReadOnlyDictionary<string, IGenerator> installed = generators.ToDictionary(
            generator => GeneratorKey(generator.Descriptor.Type, generator.Descriptor.Version),
            StringComparer.OrdinalIgnoreCase);
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (GeneratorExecutionPlanStep step in plan.Steps)
        {
            if (!installed.TryGetValue(GeneratorKey(step.Generator.Type, step.Generator.Version), out IGenerator? generator))
            {
                throw new InvalidOperationException(
                    $"Generator {step.Generator.Type} {step.Generator.Version} is not installed.");
            }

            if (generator is not IGeneratorReplayDependencyProvider provider)
            {
                continue;
            }

            foreach (string variableName in provider.GetReplayEnvironmentVariables(step.Configuration)
                         .Distinct(StringComparer.Ordinal))
            {
                if (string.IsNullOrWhiteSpace(variableName))
                {
                    throw new InvalidOperationException(
                        $"Generator step '{step.Id}' declares an empty replay environment variable.");
                }

                string value = Environment.GetEnvironmentVariable(variableName) ?? string.Empty;
                if (string.IsNullOrEmpty(value))
                {
                    throw new InvalidOperationException(
                        $"Replay dependency environment variable '{variableName}' is empty or missing.");
                }

                string dependencyId = $"{step.Id}\u001f{variableName}";
                result.Add(
                    dependencyId,
                    PrimaryKeyFingerprint.Compute($"{dependencyId}\u001f{value}", checkpointSecret));
            }
        }

        return result;
    }

    public static bool Matches(
        IReadOnlyDictionary<string, string> expected,
        IReadOnlyDictionary<string, string> actual) =>
        expected.Count == actual.Count
        && expected.All(item => actual.TryGetValue(item.Key, out string? value)
                                && string.Equals(value, item.Value, StringComparison.OrdinalIgnoreCase));

    private static string GeneratorKey(string type, string version) => $"{type}\u001f{version}";
}
