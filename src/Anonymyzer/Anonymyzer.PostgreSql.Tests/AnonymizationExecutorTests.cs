namespace Anonymyzer.PostgreSql.Tests;

using Anonymyzer.Base.Generation;
using Anonymyzer.Console.Planning;
using Anonymyzer.Generators.Simple;

public sealed class AnonymizationExecutorTests
{
    private const string CheckpointSecret = "test-checkpoint-secret-with-sufficient-entropy";

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
    public async Task ResumesByReplayingDeterministicRowSessionWithoutWritingCommittedRowsAgain()
    {
        var generator = new SequentialTextGenerator();
        var table = new GeneratorTableReference("public", "people");
        var binding = new GeneratorBinding(
            table,
            new Dictionary<string, string> { [SequentialTextGenerator.ValueOutput] = "name" });
        var step = new GeneratorExecutionPlanStep(
            "public.people/column:name",
            table,
            generator.Descriptor,
            binding,
            new SequentialTextGeneratorConfiguration
            {
                Prefix = "anon-",
                StartAt = 1,
                MinimumDigits = 2,
                PreserveNulls = false
            },
            Array.Empty<GeneratorDataRequirement>(),
            2);
        var plan = new AnonymizationExecutionPlan(2, [step]);
        var writeSlice = new ExecutionWriteSliceAssessment(true, "ready", table, "id");
        var store = new FakeExecutionRowStore(
        [
            new ExecutionSourceRow(1, new Dictionary<string, object?> { ["name"] = "Ada" }),
            new ExecutionSourceRow(2, new Dictionary<string, object?> { ["name"] = "Grace" }),
            new ExecutionSourceRow(3, new Dictionary<string, object?> { ["name"] = "Margaret" })
        ]);

        await Assert.ThrowsAsync<SimulatedInterruptionException>(() =>
            new AnonymizationExecutor([generator]).ExecuteWithResultAsync(
                plan,
                writeSlice,
                store,
                (_, _) => throw new SimulatedInterruptionException(),
                TestContext.Current.CancellationToken));
        Assert.Equal([2], store.WrittenBatchSizes);

        AnonymizationExecutionResult result = await new AnonymizationExecutor([generator])
            .ExecuteWithResumeAsync(
                plan,
                writeSlice,
                store,
                new AnonymizationExecutionResumeState(
                    2,
                    1,
                    PrimaryKeyFingerprint.Compute(2, CheckpointSecret),
                    CheckpointSecret),
                cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(3, result.ProcessedRows);
        Assert.Equal(2, result.CommittedBatches);
        Assert.Equal([2, 1], store.WrittenBatchSizes);
        Assert.Equal(["anon-01", "anon-02", "anon-03"], store.Rows.Select(row => row.Values["name"]));
    }

    [Fact]
    public async Task RefusesResumeWhenPrimaryKeyBoundaryChanged()
    {
        var generator = new FixedTextGenerator();
        var table = new GeneratorTableReference("public", "people");
        var binding = new GeneratorBinding(
            table,
            new Dictionary<string, string> { [FixedTextGenerator.ValueOutput] = "name" });
        var plan = new AnonymizationExecutionPlan(
            2,
            [new GeneratorExecutionPlanStep(
                "public.people/column:name",
                table,
                generator.Descriptor,
                binding,
                new FixedTextGeneratorConfiguration { Value = "MASKED" },
                Array.Empty<GeneratorDataRequirement>(),
                2)]);
        var store = new FakeExecutionRowStore(
        [
            new ExecutionSourceRow(0, new Dictionary<string, object?> { ["name"] = "Inserted" }),
            new ExecutionSourceRow(1, new Dictionary<string, object?> { ["name"] = "Ada" }),
            new ExecutionSourceRow(2, new Dictionary<string, object?> { ["name"] = "Grace" })
        ]);

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new AnonymizationExecutor([generator]).ExecuteWithResumeAsync(
                plan,
                new ExecutionWriteSliceAssessment(true, "ready", table, "id"),
                store,
                new AnonymizationExecutionResumeState(
                    2,
                    1,
                    PrimaryKeyFingerprint.Compute(2, CheckpointSecret),
                    CheckpointSecret),
                cancellationToken: TestContext.Current.CancellationToken));

        Assert.Contains("boundary", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(store.WrittenBatchSizes);
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

    [Fact]
    public async Task AppliesConsistentPseudonymsFromValidatedLookupTable()
    {
        var generator = new ReferencePseudonymGenerator();
        var target = new GeneratorTableReference("public", "employees");
        var lookup = new GeneratorTableReference("public", "departments");
        string keyEnvironmentVariable = $"ANONYMYZER_TEST_PSEUDONYM_{Guid.NewGuid():N}";
        Environment.SetEnvironmentVariable(keyEnvironmentVariable, "test-key-with-more-than-thirty-two-characters");
        var configuration = new ReferencePseudonymGeneratorConfiguration
        {
            ReferenceColumn = "department_id",
            LookupSchema = lookup.SchemaName,
            LookupTable = lookup.TableName,
            LookupKeyColumn = "id",
            Prefix = "department-",
            KeyEnvironmentVariable = keyEnvironmentVariable,
            HashLength = 16
        };
        var binding = new GeneratorBinding(
            target,
            new Dictionary<string, string> { [ReferencePseudonymGenerator.ValueOutput] = "department_alias" });
        var step = new GeneratorExecutionPlanStep(
            "public.employees/column:department_alias",
            target,
            generator.Descriptor,
            binding,
            configuration,
            generator.GetDataRequirements(binding, configuration),
            2);
        var store = new RelationalFakeExecutionRowStore(
            target,
            lookup,
            [
                new ExecutionSourceRow(1, new Dictionary<string, object?>
                {
                    ["department_id"] = 10,
                    ["department_alias"] = null
                }),
                new ExecutionSourceRow(2, new Dictionary<string, object?>
                {
                    ["department_id"] = 20,
                    ["department_alias"] = null
                }),
                new ExecutionSourceRow(3, new Dictionary<string, object?>
                {
                    ["department_id"] = 10,
                    ["department_alias"] = null
                })
            ],
            [
                new ExecutionSourceRow(10, new Dictionary<string, object?> { ["id"] = 10 }),
                new ExecutionSourceRow(20, new Dictionary<string, object?> { ["id"] = 20 })
            ]);
        var writeSlice = new ExecutionWriteSliceAssessment(true, "ready", target, "id")
        {
            ReadPrimaryKeys = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["public\u001fdepartments"] = "id"
            }
        };

        try
        {
            long processed = await new AnonymizationExecutor([generator]).ExecuteAsync(
                new AnonymizationExecutionPlan(2, [step]),
                writeSlice,
                store,
                TestContext.Current.CancellationToken);

            Assert.Equal(3, processed);
            Assert.Equal(store.TargetRows[0].Values["department_alias"], store.TargetRows[2].Values["department_alias"]);
            Assert.NotEqual(store.TargetRows[0].Values["department_alias"], store.TargetRows[1].Values["department_alias"]);
            Assert.All(store.TargetRows, row => Assert.StartsWith("department-", row.Values["department_alias"] as string));
            Assert.Contains(store.ReadTables, table => table.TableName == "departments");
        }
        finally
        {
            Environment.SetEnvironmentVariable(keyEnvironmentVariable, null);
        }
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

    private sealed class SimulatedInterruptionException : Exception;

    private sealed class RelationalFakeExecutionRowStore(
        GeneratorTableReference targetTable,
        GeneratorTableReference lookupTable,
        IEnumerable<ExecutionSourceRow> targetRows,
        IEnumerable<ExecutionSourceRow> lookupRows) : IExecutionRowStore
    {
        private readonly List<ExecutionSourceRow> _targetRows = targetRows.ToList();
        private readonly List<ExecutionSourceRow> _lookupRows = lookupRows.ToList();

        public IReadOnlyList<ExecutionSourceRow> TargetRows => _targetRows;

        public List<GeneratorTableReference> ReadTables { get; } = new();

        public Task<IReadOnlyList<ExecutionSourceRow>> ReadNextBatchAsync(
            GeneratorTableReference table,
            string primaryKeyColumn,
            IReadOnlyList<string> columns,
            object? afterPrimaryKey,
            int batchSize,
            CancellationToken cancellationToken)
        {
            ReadTables.Add(table);
            List<ExecutionSourceRow> source = TableKey(table) switch
            {
                var key when key == TableKey(targetTable) => _targetRows,
                var key when key == TableKey(lookupTable) => _lookupRows,
                _ => throw new InvalidOperationException("Unexpected table read in relational executor test.")
            };
            int after = afterPrimaryKey is null ? int.MinValue : Convert.ToInt32(afterPrimaryKey);
            IReadOnlyList<ExecutionSourceRow> result = source
                .Where(row => Convert.ToInt32(row.PrimaryKey) > after)
                .OrderBy(row => Convert.ToInt32(row.PrimaryKey))
                .Take(batchSize)
                .Select(row => new ExecutionSourceRow(
                    row.PrimaryKey,
                    new Dictionary<string, object?>(row.Values, StringComparer.OrdinalIgnoreCase)))
                .ToArray();
            return Task.FromResult(result);
        }

        public Task WriteBatchAsync(
            GeneratorTableReference table,
            string primaryKeyColumn,
            IReadOnlyList<ExecutionOutputColumn> outputColumns,
            IReadOnlyList<ExecutionUpdatedRow> rows,
            CancellationToken cancellationToken)
        {
            Assert.Equal(TableKey(targetTable), TableKey(table));
            foreach (ExecutionUpdatedRow updated in rows)
            {
                int index = _targetRows.FindIndex(row => Equals(row.PrimaryKey, updated.PrimaryKey));
                var values = new Dictionary<string, object?>(_targetRows[index].Values, StringComparer.OrdinalIgnoreCase);
                foreach (ExecutionOutputColumn output in outputColumns)
                {
                    values[output.Name] = updated.Values[output.Name];
                }

                _targetRows[index] = new ExecutionSourceRow(updated.PrimaryKey, values);
            }

            return Task.CompletedTask;
        }

        private static string TableKey(GeneratorTableReference table) =>
            $"{table.SchemaName}\u001f{table.TableName}";
    }
}
