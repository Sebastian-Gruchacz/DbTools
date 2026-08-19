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
        CancellationToken cancellationToken = default) =>
        (await ExecuteWithResultAsync(plan, writeSlice, store, cancellationToken: cancellationToken)).ProcessedRows;

    public async Task<AnonymizationExecutionResult> ExecuteWithResultAsync(
        AnonymizationExecutionPlan plan,
        ExecutionWriteSliceAssessment writeSlice,
        IExecutionRowStore store,
        Func<AnonymizationExecutionProgress, CancellationToken, Task>? batchCommitted = null,
        CancellationToken cancellationToken = default) =>
        await ExecuteCoreAsync(
            plan,
            writeSlice,
            store,
            resumeState: null,
            batchCommitted,
            cancellationToken);

    public async Task<AnonymizationExecutionResult> ExecuteWithResumeAsync(
        AnonymizationExecutionPlan plan,
        ExecutionWriteSliceAssessment writeSlice,
        IExecutionRowStore store,
        AnonymizationExecutionResumeState resumeState,
        Func<AnonymizationExecutionProgress, CancellationToken, Task>? batchCommitted = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(resumeState);
        ExecutionResumeSafetyAssessment safety = new ExecutionResumeSafetyAssessor().Assess(plan);
        if (!safety.IsSupported)
        {
            throw new InvalidOperationException(safety.Message + ".");
        }

        if (resumeState.ProcessedRows < 0 || resumeState.CommittedBatches < 0)
        {
            throw new InvalidOperationException("Resume counters cannot be negative.");
        }

        return await ExecuteCoreAsync(
            plan,
            writeSlice,
            store,
            resumeState,
            batchCommitted,
            cancellationToken);
    }

    private async Task<AnonymizationExecutionResult> ExecuteCoreAsync(
        AnonymizationExecutionPlan plan,
        ExecutionWriteSliceAssessment writeSlice,
        IExecutionRowStore store,
        AnonymizationExecutionResumeState? resumeState,
        Func<AnonymizationExecutionProgress, CancellationToken, Task>? batchCommitted,
        CancellationToken cancellationToken)
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
            ExecutionOutputColumn[] outputColumns = plan.Steps
                .SelectMany(step => step.Binding.Outputs.Select(output => new ExecutionOutputColumn(
                    output.Value,
                    step.Binding.GetOutputDataType(output.Key))))
                .DistinctBy(column => column.Name, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            string[] sourceColumns = outputColumns
                .Select(column => column.Name)
                .Concat(plan.Steps.SelectMany(step => step.DataRequirements).SelectMany(requirement => requirement.Columns))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            object? afterPrimaryKey = resumeState is null
                ? null
                : await ReplayCommittedRowsAsync(
                    plan,
                    writeSlice,
                    store,
                    sessions,
                    sourceColumns,
                    resumeState,
                    cancellationToken);
            long processedRows = resumeState?.ProcessedRows ?? 0;
            int committedBatches = resumeState?.CommittedBatches ?? 0;

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
                    return new AnonymizationExecutionResult(
                        processedRows,
                        committedBatches,
                        afterPrimaryKey);
                }

                List<ExecutionUpdatedRow> updatedRows = await ApplyRowsAsync(
                    plan,
                    sessions,
                    sourceRows,
                    outputColumns,
                    cancellationToken);

                await store.WriteBatchAsync(
                    writeSlice.TargetTable,
                    writeSlice.PrimaryKeyColumn,
                    outputColumns,
                    updatedRows,
                    cancellationToken);
                processedRows += updatedRows.Count;
                afterPrimaryKey = sourceRows[^1].PrimaryKey;
                committedBatches++;
                if (batchCommitted is not null)
                {
                    await batchCommitted(
                        new AnonymizationExecutionProgress(
                            processedRows,
                            committedBatches,
                            afterPrimaryKey,
                            updatedRows.Count),
                        cancellationToken);
                }
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

    private static async Task<object?> ReplayCommittedRowsAsync(
        AnonymizationExecutionPlan plan,
        ExecutionWriteSliceAssessment writeSlice,
        IExecutionRowStore store,
        IReadOnlyList<IGeneratorSession> sessions,
        IReadOnlyList<string> sourceColumns,
        AnonymizationExecutionResumeState resumeState,
        CancellationToken cancellationToken)
    {
        long remainingRows = resumeState.ProcessedRows;
        int replayedBatches = 0;
        object? afterPrimaryKey = null;
        while (remainingRows > 0)
        {
            IReadOnlyList<ExecutionSourceRow> rows = await store.ReadNextBatchAsync(
                writeSlice.TargetTable!,
                writeSlice.PrimaryKeyColumn!,
                sourceColumns,
                afterPrimaryKey,
                plan.BatchSize,
                cancellationToken);
            if (rows.Count == 0 || rows.Count > remainingRows)
            {
                throw new InvalidOperationException(
                    "Checkpoint counters do not match the current target rows and cannot be resumed safely.");
            }

            await ApplyRowsAsync(plan, sessions, rows, outputColumns: null, cancellationToken);
            remainingRows -= rows.Count;
            replayedBatches++;
            afterPrimaryKey = rows[^1].PrimaryKey;
        }

        if (replayedBatches != resumeState.CommittedBatches)
        {
            throw new InvalidOperationException(
                "Checkpoint batch count does not match the replayed target rows and cannot be resumed safely.");
        }

        if (resumeState.ProcessedRows > 0
            && !PrimaryKeyFingerprint.Compute(afterPrimaryKey, resumeState.PrimaryKeyFingerprintSecret).Equals(
                resumeState.LastPrimaryKeyHmacSha256,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Checkpoint primary-key boundary does not match the current target rows and cannot be resumed safely.");
        }

        return afterPrimaryKey;
    }

    private static async Task<List<ExecutionUpdatedRow>> ApplyRowsAsync(
        AnonymizationExecutionPlan plan,
        IReadOnlyList<IGeneratorSession> sessions,
        IReadOnlyList<ExecutionSourceRow> sourceRows,
        IReadOnlyList<ExecutionOutputColumn>? outputColumns,
        CancellationToken cancellationToken)
    {
        var updatedRows = new List<ExecutionUpdatedRow>(outputColumns is null ? 0 : sourceRows.Count);
        foreach (ExecutionSourceRow sourceRow in sourceRows)
        {
            var row = new MutableExecutionRow(sourceRow);
            for (int index = 0; index < plan.Steps.Count; index++)
            {
                await sessions[index].ApplyAsync(row.ForStep(plan.Steps[index]), cancellationToken);
            }

            if (outputColumns is not null)
            {
                updatedRows.Add(new ExecutionUpdatedRow(
                    sourceRow.PrimaryKey,
                    outputColumns.ToDictionary(
                        column => column.Name,
                        column => row.GetCurrentValue(column.Name),
                        StringComparer.OrdinalIgnoreCase)));
            }
        }

        return updatedRows;
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

internal sealed record AnonymizationExecutionProgress(
    long ProcessedRows,
    int CommittedBatches,
    object? LastPrimaryKey,
    int LastBatchSize);

internal sealed record AnonymizationExecutionResult(
    long ProcessedRows,
    int CommittedBatches,
    object? LastPrimaryKey);

internal sealed record AnonymizationExecutionResumeState(
    long ProcessedRows,
    int CommittedBatches,
    string LastPrimaryKeyHmacSha256,
    string PrimaryKeyFingerprintSecret);
