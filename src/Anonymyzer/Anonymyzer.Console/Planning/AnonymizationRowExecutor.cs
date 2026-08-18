namespace Anonymyzer.Console.Planning;

using Anonymyzer.Base.Generation;

internal sealed class AnonymizationRowExecutor(IEnumerable<IGenerator> generators)
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

        IGeneratorSession[] sessions = await PrepareSessionsAsync(plan, cancellationToken);
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
        CancellationToken cancellationToken)
    {
        var sessions = new List<IGeneratorSession>(plan.Steps.Count);
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
                    new GeneratorPreparationContext(step.Binding, new RejectingDataReader()),
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

    private sealed class RejectingDataReader : IGeneratorDataReader
    {
        public IAsyncEnumerable<GeneratorDataRow> ReadAsync(
            GeneratorDataRequirement requirement,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException(
                "The first Row write slice cannot prepare a generator from a database scan.");
        }
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
