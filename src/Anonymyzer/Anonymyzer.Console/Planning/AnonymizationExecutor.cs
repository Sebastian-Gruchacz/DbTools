namespace Anonymyzer.Console.Planning;

using Anonymyzer.Base.Generation;

internal sealed class AnonymizationExecutor(IEnumerable<IGenerator> generators)
{
    private readonly IReadOnlyDictionary<string, IGenerator> _generators = generators.ToDictionary(
        generator => GeneratorKey(generator.Descriptor.Type, generator.Descriptor.Version),
        StringComparer.OrdinalIgnoreCase);

    public async Task<long> ExecuteAsync(
        AnonymizationExecutionPlan plan,
        ExecutionWriteSliceAssessment writeSlice,
        IExecutionRowStore store,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(writeSlice);
        ArgumentNullException.ThrowIfNull(store);
        if (!writeSlice.IsSupported
            || writeSlice.TargetTable is null
            || string.IsNullOrWhiteSpace(writeSlice.PrimaryKeyColumn))
        {
            throw new InvalidOperationException($"Execution plan is not write-ready: {writeSlice.Message}.");
        }

        IGeneratorSession[] sessions = await PrepareSessionsAsync(
            plan,
            writeSlice,
            store,
            cancellationToken);
        try
        {
            string[] outputColumns = plan.Steps
                .SelectMany(step => step.Binding.Outputs.Values)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            string[] sourceColumns = outputColumns
                .Concat(plan.Steps.SelectMany(step => step.DataRequirements).SelectMany(requirement => requirement.Columns))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            object? afterPrimaryKey = null;
            long processedRows = 0;

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                IReadOnlyList<ExecutionSourceRow> sourceRows = await store.ReadNextBatchAsync(
                    writeSlice.TargetTable,
                    writeSlice.PrimaryKeyColumn,
                    sourceColumns,
                    afterPrimaryKey,
                    plan.BatchSize,
                    cancellationToken);
                if (sourceRows.Count == 0)
                {
                    return processedRows;
                }

                var updatedRows = new List<ExecutionUpdatedRow>(sourceRows.Count);
                foreach (ExecutionSourceRow sourceRow in sourceRows)
                {
                    var row = new MutableExecutionRow(sourceRow);
                    for (int index = 0; index < plan.Steps.Count; index++)
                    {
                        await sessions[index].ApplyAsync(row.ForStep(plan.Steps[index]), cancellationToken);
                    }

                    updatedRows.Add(new ExecutionUpdatedRow(
                        sourceRow.PrimaryKey,
                        outputColumns.ToDictionary(column => column, row.GetCurrentValue, StringComparer.OrdinalIgnoreCase)));
                }

                await store.WriteBatchAsync(
                    writeSlice.TargetTable,
                    writeSlice.PrimaryKeyColumn,
                    outputColumns,
                    updatedRows,
                    cancellationToken);
                processedRows += updatedRows.Count;
                afterPrimaryKey = sourceRows[^1].PrimaryKey;
            }
        }
        finally
        {
            foreach (IGeneratorSession session in sessions.Reverse())
            {
                await session.DisposeAsync();
            }
        }
    }

    private async Task<IGeneratorSession[]> PrepareSessionsAsync(
        AnonymizationExecutionPlan plan,
        ExecutionWriteSliceAssessment writeSlice,
        IExecutionRowStore store,
        CancellationToken cancellationToken)
    {
        var sessions = new List<IGeneratorSession>(plan.Steps.Count);
        var dataReader = new StoreGeneratorDataReader(
            store,
            writeSlice.TargetTable!,
            writeSlice.PrimaryKeyColumn!,
            plan.BatchSize);
        try
        {
            foreach (GeneratorExecutionPlanStep step in plan.Steps)
            {
                IGenerator generator = _generators.TryGetValue(
                    GeneratorKey(step.Generator.Type, step.Generator.Version),
                    out IGenerator? installed)
                    ? installed
                    : throw new InvalidOperationException(
                        $"Generator {step.Generator.Type} {step.Generator.Version} is not installed.");
                sessions.Add(await generator.PrepareAsync(
                    new GeneratorPreparationContext(step.Binding, dataReader),
                    step.Configuration,
                    cancellationToken));
            }

            return sessions.ToArray();
        }
        catch
        {
            foreach (IGeneratorSession session in sessions.AsEnumerable().Reverse())
            {
                await session.DisposeAsync();
            }

            throw;
        }
    }

    private static string GeneratorKey(string type, string version) => $"{type}\u001f{version}";

    private sealed class StoreGeneratorDataReader(
        IExecutionRowStore store,
        GeneratorTableReference targetTable,
        string primaryKeyColumn,
        int batchSize) : IGeneratorDataReader
    {
        public async IAsyncEnumerable<GeneratorDataRow> ReadAsync(
            GeneratorDataRequirement requirement,
            [System.Runtime.CompilerServices.EnumeratorCancellation]
            CancellationToken cancellationToken = default)
        {
            if (!TableKey(requirement.Table).Equals(TableKey(targetTable), StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Generator data requirement '{requirement.Alias}' reads outside the validated target table.");
            }

            object? afterPrimaryKey = null;
            while (true)
            {
                IReadOnlyList<ExecutionSourceRow> rows = await store.ReadNextBatchAsync(
                    targetTable,
                    primaryKeyColumn,
                    requirement.Columns,
                    afterPrimaryKey,
                    batchSize,
                    cancellationToken);
                if (rows.Count == 0)
                {
                    yield break;
                }

                foreach (ExecutionSourceRow row in rows)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    yield return new GeneratorDataRow(row.Values);
                }

                afterPrimaryKey = rows[^1].PrimaryKey;
            }
        }

        private static string TableKey(GeneratorTableReference table) =>
            $"{table.SchemaName}\u001f{table.TableName}";
    }

    private sealed class MutableExecutionRow(ExecutionSourceRow sourceRow)
    {
        private readonly IReadOnlyDictionary<string, object?> _original = sourceRow.Values;
        private readonly Dictionary<string, object?> _current = new(sourceRow.Values, StringComparer.OrdinalIgnoreCase);

        public object? GetCurrentValue(string columnName) =>
            _current.TryGetValue(columnName, out object? value) ? value : null;

        public IGeneratorRow ForStep(GeneratorExecutionPlanStep step) => new StepRow(this, step);

        private sealed class StepRow(
            MutableExecutionRow owner,
            GeneratorExecutionPlanStep step) : IGeneratorRow
        {
            public object? GetValue(string columnName)
            {
                bool requiresOriginal = step.DataRequirements.Any(requirement =>
                    requirement.ValueSource == GeneratorValueSource.Original
                    && requirement.Columns.Contains(columnName, StringComparer.OrdinalIgnoreCase));
                IReadOnlyDictionary<string, object?> values = requiresOriginal ? owner._original : owner._current;
                return values.TryGetValue(columnName, out object? value) ? value : null;
            }

            public void SetValue(string columnName, object? value)
            {
                if (!step.Binding.Outputs.Values.Contains(columnName, StringComparer.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        $"Generator step '{step.Id}' attempted to write unbound column '{columnName}'.");
                }

                owner._current[columnName] = value;
            }
        }
    }
}
