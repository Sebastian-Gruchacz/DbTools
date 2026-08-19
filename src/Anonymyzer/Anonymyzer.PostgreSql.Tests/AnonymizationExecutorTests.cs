namespace Anonymyzer.PostgreSql.Tests;

using Anonymyzer.Base.Generation;
using Anonymyzer.Console.Planning;
using Anonymyzer.Generators.Simple;

public sealed class AnonymizationExecutorTests
{
    [Fact]
    public async Task ProcessesRowsInKeysetBatchesAndWritesGeneratedValues()
    {
        var generator = new FixedTextGenerator();
        var table = new GeneratorTableReference("public", "people");
        var binding = new GeneratorBinding(
            table,
            new Dictionary<string, string> { [FixedTextGenerator.ValueOutput] = "name" });
        var step = new GeneratorExecutionPlanStep(
            "public.people/column:name",
            table,
            generator.Descriptor,
            binding,
            new FixedTextGeneratorConfiguration { Value = "MASKED", PreserveNulls = false },
            Array.Empty<GeneratorDataRequirement>(),
            2);
        var plan = new AnonymizationExecutionPlan(2, [step]);
        var writeSlice = new ExecutionWriteSliceAssessment(true, "ready", table, "id");
        var store = new FakeExecutionRowStore(
        [
            new ExecutionSourceRow(1, new Dictionary<string, object?> { ["id"] = 1, ["name"] = "Ada" }),
            new ExecutionSourceRow(2, new Dictionary<string, object?> { ["id"] = 2, ["name"] = null }),
            new ExecutionSourceRow(3, new Dictionary<string, object?> { ["id"] = 3, ["name"] = "Grace" })
        ]);

        long processed = await new AnonymizationExecutor([generator]).ExecuteAsync(
            plan,
            writeSlice,
            store,
            TestContext.Current.CancellationToken);

        Assert.Equal(3, processed);
        Assert.Equal([2, 1], store.WrittenBatchSizes);
        Assert.All(store.Rows, row => Assert.Equal("MASKED", row.Values["name"]));
        Assert.All(store.ReadColumnSets, columns => Assert.Contains("name", columns));
    }

    [Fact]
    public async Task ReportsProgressOnlyAfterEachBatchWasCommitted()
    {
        var generator = new FixedTextGenerator();
        var table = new GeneratorTableReference("public", "people");
        var binding = new GeneratorBinding(
            table,
            new Dictionary<string, string> { [FixedTextGenerator.ValueOutput] = "name" });
        var step = new GeneratorExecutionPlanStep(
            "public.people/column:name",
            table,
            generator.Descriptor,
            binding,
            new FixedTextGeneratorConfiguration { Value = "MASKED", PreserveNulls = false },
            Array.Empty<GeneratorDataRequirement>(),
            2);
        var store = new FakeExecutionRowStore(
        [
            new ExecutionSourceRow(1, new Dictionary<string, object?> { ["name"] = "Ada" }),
            new ExecutionSourceRow(2, new Dictionary<string, object?> { ["name"] = "Grace" }),
            new ExecutionSourceRow(3, new Dictionary<string, object?> { ["name"] = "Margaret" })
        ]);
        var progress = new List<AnonymizationExecutionProgress>();

        AnonymizationExecutionResult result = await new AnonymizationExecutor([generator])
            .ExecuteWithResultAsync(
                new AnonymizationExecutionPlan(2, [step]),
                new ExecutionWriteSliceAssessment(true, "ready", table, "id"),
                store,
                (update, _) =>
                {
                    Assert.Equal(store.WrittenBatchSizes.Count, update.CommittedBatches);
                    progress.Add(update);
                    return Task.CompletedTask;
                },
                TestContext.Current.CancellationToken);

        Assert.Equal(3, result.ProcessedRows);
        Assert.Equal(2, result.CommittedBatches);
        Assert.Equal(3, result.LastPrimaryKey);
        Assert.Collection(
            progress,
            first =>
            {
                Assert.Equal(2, first.ProcessedRows);
                Assert.Equal(2, first.LastBatchSize);
                Assert.Equal(2, first.LastPrimaryKey);
            },
            second =>
            {
                Assert.Equal(3, second.ProcessedRows);
                Assert.Equal(1, second.LastBatchSize);
                Assert.Equal(3, second.LastPrimaryKey);
            });
    }

    [Fact]
    public async Task RefusesPlanThatDidNotPassWriteSliceValidation()
    {
        var generator = new FixedTextGenerator();
        var plan = new AnonymizationExecutionPlan(10, Array.Empty<GeneratorExecutionPlanStep>());
        var store = new FakeExecutionRowStore(Array.Empty<ExecutionSourceRow>());

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new AnonymizationExecutor([generator]).ExecuteAsync(
                plan,
                new ExecutionWriteSliceAssessment(false, "no primary key", null, null),
                store,
                TestContext.Current.CancellationToken));

        Assert.Contains("not write-ready", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(store.WrittenBatchSizes);
    }

    [Fact]
    public async Task PreparesColumnShufflerFromCompleteOriginalColumnBeforeWritingBatches()
    {
        var generator = new ShufflingTextGenerator();
        var table = new GeneratorTableReference("public", "people");
        var binding = new GeneratorBinding(
            table,
            new Dictionary<string, string> { [ShufflingTextGenerator.ValueOutput] = "name" });
        GeneratorDataRequirement requirement = generator.GetDataRequirements(
            binding,
            new ShufflingTextGeneratorConfiguration()).Single();
        var step = new GeneratorExecutionPlanStep(
            "public.people/column:name",
            table,
            generator.Descriptor,
            binding,
            new ShufflingTextGeneratorConfiguration { Seed = 42, MinimumPopulation = 2 },
            [requirement],
            2);
        var plan = new AnonymizationExecutionPlan(2, [step]);
        var store = new FakeExecutionRowStore(
        [
            new ExecutionSourceRow(1, new Dictionary<string, object?> { ["id"] = 1, ["name"] = "Ada" }),
            new ExecutionSourceRow(2, new Dictionary<string, object?> { ["id"] = 2, ["name"] = "Grace" }),
            new ExecutionSourceRow(3, new Dictionary<string, object?> { ["id"] = 3, ["name"] = "Margaret" })
        ]);

        long processed = await new AnonymizationExecutor([generator]).ExecuteAsync(
            plan,
            new ExecutionWriteSliceAssessment(true, "ready", table, "id"),
            store,
            TestContext.Current.CancellationToken);

        Assert.Equal(3, processed);
        Assert.Equal([2, 1], store.WrittenBatchSizes);
        Assert.Equal(
            ["Ada", "Grace", "Margaret"],
            store.Rows.Select(row => (string)row.Values["name"]!).Order().ToArray());
        Assert.NotEqual(
            ["Ada", "Grace", "Margaret"],
            store.Rows.Select(row => (string)row.Values["name"]!).ToArray());
        Assert.Equal(6, store.ReadColumnSets.Count);
    }

    private sealed class FakeExecutionRowStore(IEnumerable<ExecutionSourceRow> rows) : IExecutionRowStore
    {
        private readonly List<ExecutionSourceRow> _rows = rows.ToList();

        public IReadOnlyList<ExecutionSourceRow> Rows => _rows;

        public List<int> WrittenBatchSizes { get; } = new();

        public List<IReadOnlyList<string>> ReadColumnSets { get; } = new();

        public Task<IReadOnlyList<ExecutionSourceRow>> ReadNextBatchAsync(
            GeneratorTableReference table,
            string primaryKeyColumn,
            IReadOnlyList<string> columns,
            object? afterPrimaryKey,
            int batchSize,
            CancellationToken cancellationToken)
        {
            ReadColumnSets.Add(columns.ToArray());
            int after = afterPrimaryKey is null ? int.MinValue : Convert.ToInt32(afterPrimaryKey);
            IReadOnlyList<ExecutionSourceRow> batch = _rows
                .Where(row => Convert.ToInt32(row.PrimaryKey) > after)
                .OrderBy(row => Convert.ToInt32(row.PrimaryKey))
                .Take(batchSize)
                .Select(row => new ExecutionSourceRow(
                    row.PrimaryKey,
                    new Dictionary<string, object?>(row.Values, StringComparer.OrdinalIgnoreCase)))
                .ToArray();
            return Task.FromResult(batch);
        }

        public Task WriteBatchAsync(
            GeneratorTableReference table,
            string primaryKeyColumn,
            IReadOnlyList<ExecutionOutputColumn> outputColumns,
            IReadOnlyList<ExecutionUpdatedRow> rows,
            CancellationToken cancellationToken)
        {
            WrittenBatchSizes.Add(rows.Count);
            foreach (ExecutionUpdatedRow updated in rows)
            {
                int index = _rows.FindIndex(row => Equals(row.PrimaryKey, updated.PrimaryKey));
                var values = new Dictionary<string, object?>(_rows[index].Values, StringComparer.OrdinalIgnoreCase);
                foreach (ExecutionOutputColumn outputColumn in outputColumns)
                {
                    string column = outputColumn.Name;
                    values[column] = updated.Values[column];
                }

                _rows[index] = new ExecutionSourceRow(updated.PrimaryKey, values);
            }

            return Task.CompletedTask;
        }
    }
}
