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

    [Fact]
    public async Task UsesEncryptedTemporaryIndexAfterMemoryLimitAndDeletesItOnDispose()
    {
        string keyEnvironmentVariable = $"ANONYMYZER_TEST_PSEUDONYM_{Guid.NewGuid():N}";
        Environment.SetEnvironmentVariable(keyEnvironmentVariable, "spill-test-key-with-more-than-thirty-two-characters");
        string temporaryDirectory = Path.Combine(Path.GetTempPath(), "Anonymyzer");
        string[] filesBefore = Directory.Exists(temporaryDirectory)
            ? Directory.GetFiles(temporaryDirectory, "reference-index-*.bin")
            : Array.Empty<string>();
        try
        {
            var generator = new ReferencePseudonymGenerator();
            ReferencePseudonymGeneratorConfiguration configuration = CreateConfiguration();
            configuration.KeyEnvironmentVariable = keyEnvironmentVariable;
            configuration.MaximumInMemoryBytes = 1024 * 1024;
            configuration.OverflowStrategy = RelationalLookupOverflowStrategy.EncryptedTemporaryIndex;
            IGeneratorSession session = await generator.PrepareAsync(
                new GeneratorPreparationContext(CreateBinding(), new LookupReader(Enumerable.Range(1, 5000).Cast<object>())),
                configuration,
                TestContext.Current.CancellationToken);
            try
            {
                string[] filesDuringSession = Directory.GetFiles(temporaryDirectory, "reference-index-*.bin");
                Assert.True(filesDuringSession.Except(filesBefore, StringComparer.OrdinalIgnoreCase).Any());

                var row = new MutableRow(new Dictionary<string, object?> { ["DepartmentId"] = 4999 });
                await session.ApplyAsync(row, TestContext.Current.CancellationToken);
                Assert.StartsWith("anon-", row.GetValue("DepartmentAlias") as string);
            }
            finally
            {
                await session.DisposeAsync();
            }

            string[] filesAfter = Directory.GetFiles(temporaryDirectory, "reference-index-*.bin");
            Assert.Empty(filesAfter.Except(filesBefore, StringComparer.OrdinalIgnoreCase));
        }
        finally
        {
            Environment.SetEnvironmentVariable(keyEnvironmentVariable, null);
        }
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

    private sealed class LookupReader : IGeneratorDataReader
    {
        private readonly IEnumerable<object> _values;

        public LookupReader(object value) : this([value])
        {
        }

        public LookupReader(IEnumerable<object> values)
        {
            _values = values;
        }

        public async IAsyncEnumerable<GeneratorDataRow> ReadAsync(
            GeneratorDataRequirement requirement,
            [System.Runtime.CompilerServices.EnumeratorCancellation]
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Yield();
            foreach (object value in _values)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return new GeneratorDataRow(new Dictionary<string, object?>
                {
                    [requirement.Columns.Single()] = value
                });
            }
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

    private sealed class MutableRow(IReadOnlyDictionary<string, object?> values) : IGeneratorRow
    {
        private readonly Dictionary<string, object?> _values = new(values, StringComparer.OrdinalIgnoreCase);

        public object? GetValue(string columnName) =>
            _values.TryGetValue(columnName, out object? value) ? value : null;

        public void SetValue(string columnName, object? value) => _values[columnName] = value;
    }
}
