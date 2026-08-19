namespace Anonymyzer.PostgreSql.Tests;

using Anonymyzer.Base.Generation;
using Anonymyzer.Generators.Simple;

public sealed class ReferencePseudonymGeneratorTests
{
    [Fact]
    public async Task RequiresSecretFromNamedEnvironmentVariable()
    {
        var generator = new ReferencePseudonymGenerator();
        ReferencePseudonymGeneratorConfiguration configuration = CreateConfiguration();
        configuration.KeyEnvironmentVariable = $"ANONYMYZER_MISSING_{Guid.NewGuid():N}";
        GeneratorBinding binding = CreateBinding();

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await generator.PrepareAsync(
                new GeneratorPreparationContext(binding, new LookupReader(10)),
                configuration,
                TestContext.Current.CancellationToken));

        Assert.Contains(configuration.KeyEnvironmentVariable, exception.Message, StringComparison.Ordinal);
        Assert.Contains("32", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DoesNotExposeMissingReferenceValue()
    {
        string keyEnvironmentVariable = $"ANONYMYZER_TEST_PSEUDONYM_{Guid.NewGuid():N}";
        Environment.SetEnvironmentVariable(keyEnvironmentVariable, "unit-test-key-with-more-than-thirty-two-characters");
        try
        {
            var generator = new ReferencePseudonymGenerator();
            ReferencePseudonymGeneratorConfiguration configuration = CreateConfiguration();
            configuration.KeyEnvironmentVariable = keyEnvironmentVariable;
            GeneratorBinding binding = CreateBinding();
            await using IGeneratorSession session = await generator.PrepareAsync(
                new GeneratorPreparationContext(binding, new LookupReader(10)),
                configuration,
                TestContext.Current.CancellationToken);
            var row = new FakeRow(new Dictionary<string, object?> { ["DepartmentId"] = 987654321 });

            InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await session.ApplyAsync(row, TestContext.Current.CancellationToken));

            Assert.DoesNotContain("987654321", exception.Message, StringComparison.Ordinal);
            Assert.Contains("No key value was logged", exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            Environment.SetEnvironmentVariable(keyEnvironmentVariable, null);
        }
    }

    [Fact]
    public void RefusesToOverwriteReferenceColumn()
    {
        var generator = new ReferencePseudonymGenerator();
        var binding = new GeneratorBinding(
            new GeneratorTableReference("public", "employees"),
            new Dictionary<string, string> { [ReferencePseudonymGenerator.ValueOutput] = "DepartmentId" });

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            generator.GetDataRequirements(binding, CreateConfiguration()));

        Assert.Contains("cannot overwrite", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static ReferencePseudonymGeneratorConfiguration CreateConfiguration() => new()
    {
        ReferenceColumn = "DepartmentId",
        LookupSchema = "public",
        LookupTable = "departments",
        LookupKeyColumn = "Id"
    };

    private static GeneratorBinding CreateBinding() => new(
        new GeneratorTableReference("public", "employees"),
        new Dictionary<string, string> { [ReferencePseudonymGenerator.ValueOutput] = "DepartmentAlias" });

    private sealed class LookupReader(object value) : IGeneratorDataReader
    {
        public async IAsyncEnumerable<GeneratorDataRow> ReadAsync(
            GeneratorDataRequirement requirement,
            [System.Runtime.CompilerServices.EnumeratorCancellation]
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Yield();
            yield return new GeneratorDataRow(new Dictionary<string, object?>
            {
                [requirement.Columns.Single()] = value
            });
        }
    }

    private sealed class FakeRow(IReadOnlyDictionary<string, object?> values) : IGeneratorRow
    {
        public object? GetValue(string columnName) => values[columnName];

        public void SetValue(string columnName, object? value)
        {
            throw new InvalidOperationException("The failing row must not be modified.");
        }
    }
}
