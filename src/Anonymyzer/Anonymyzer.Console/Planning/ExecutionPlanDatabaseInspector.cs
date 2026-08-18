namespace Anonymyzer.Console.Planning;

using Anonymyzer.Base;
using Anonymyzer.Base.Generation;
using Anonymyzer.Configuration;

internal sealed class ExecutionPlanDatabaseInspector
{
    private const long PerValueMemoryOverhead = 32;

    public ExecutionPlanDatabaseInspection Inspect(
        AnonymizationConfiguration configuration,
        AnonymizationExecutionPlan plan,
        IAnonymyzerEngine engine)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(engine);

        Dictionary<string, ITableInfo> liveTables = engine.ListTables(listSystemTables: false)
            .ToDictionary(table => TableKey(table.SchemaName, table.Name), StringComparer.OrdinalIgnoreCase);
        var snapshots = new Dictionary<string, TableSnapshot>(StringComparer.OrdinalIgnoreCase);
        var stepInspections = new Dictionary<string, GeneratorStepDatabaseInspection>(StringComparer.OrdinalIgnoreCase);

        foreach (GeneratorExecutionPlanStep step in plan.Steps)
        {
            TableSnapshot target = GetSnapshot(step.TargetTable, engine, liveTables, snapshots);
            ValidateTargetColumns(configuration, step, target);
            var requirementEstimates = new Dictionary<string, DataRequirementEstimate>(StringComparer.OrdinalIgnoreCase);

            foreach (GeneratorDataRequirement requirement in step.DataRequirements)
            {
                TableSnapshot source = GetSnapshot(requirement.Table, engine, liveTables, snapshots);
                IReadOnlyList<IColumnInfo> columns = GetRequiredColumns(step, requirement, source);
                long? memory = requirement.RequiresCompleteScan
                    ? EstimateMaximumMemory(source.Table.EstimatedRowCount, columns)
                    : null;
                if (!requirementEstimates.TryAdd(
                        requirement.Alias,
                        new DataRequirementEstimate(source.Table.EstimatedRowCount, memory)))
                {
                    throw new InvalidOperationException(
                        $"Generator step '{step.Id}' contains duplicate data requirement alias '{requirement.Alias}'.");
                }
            }

            stepInspections.Add(
                step.Id,
                new GeneratorStepDatabaseInspection(
                    target.Table.EstimatedRowCount,
                    target.Columns.Values
                        .Where(column => column.IsPartOfThePrimaryKey)
                        .OrderBy(column => column.Ordinal)
                        .Select(column => column.Name)
                        .ToArray(),
                    requirementEstimates));
        }

        return new ExecutionPlanDatabaseInspection(stepInspections);
    }

    private static TableSnapshot GetSnapshot(
        GeneratorTableReference reference,
        IAnonymyzerEngine engine,
        IReadOnlyDictionary<string, ITableInfo> liveTables,
        IDictionary<string, TableSnapshot> snapshots)
    {
        string key = TableKey(reference.SchemaName, reference.TableName);
        if (snapshots.TryGetValue(key, out TableSnapshot? snapshot))
        {
            return snapshot;
        }

        if (!liveTables.TryGetValue(key, out ITableInfo? table))
        {
            throw new InvalidOperationException(
                $"Configured table {reference.SchemaName}.{reference.TableName} does not exist in the connected clone.");
        }

        snapshot = new TableSnapshot(
            table,
            engine.ListColumns(table).ToDictionary(column => column.Name, StringComparer.OrdinalIgnoreCase));
        snapshots.Add(key, snapshot);
        return snapshot;
    }

    private static void ValidateTargetColumns(
        AnonymizationConfiguration configuration,
        GeneratorExecutionPlanStep step,
        TableSnapshot liveTable)
    {
        TableProcessingOptions configuredTable = configuration.Tables.Single(table =>
            table.SchemaName.Equals(step.TargetTable.SchemaName, StringComparison.OrdinalIgnoreCase)
            && table.TableName.Equals(step.TargetTable.TableName, StringComparison.OrdinalIgnoreCase));

        foreach (string columnName in step.Binding.Outputs.Values)
        {
            ColumnProcessingOptions configuredColumn = configuredTable.Columns.Single(column =>
                column.ColumnName.Equals(columnName, StringComparison.OrdinalIgnoreCase));
            IColumnInfo liveColumn = GetColumn(step.Id, liveTable, columnName);
            if (!configuredColumn.DataType.Equals(liveColumn.DataType.ToString(), StringComparison.OrdinalIgnoreCase)
                || configuredColumn.MaxLength != liveColumn.MaxLength
                || configuredColumn.Unicode != liveColumn.IsUnicodeText)
            {
                throw new InvalidOperationException(
                    $"Schema drift detected for {configuredTable.SchemaName}.{configuredTable.TableName}.{columnName}: " +
                    $"configured {configuredColumn.DataType}({configuredColumn.MaxLength}), unicode={configuredColumn.Unicode}; " +
                    $"live {liveColumn.DataType}({liveColumn.MaxLength}), unicode={liveColumn.IsUnicodeText}.");
            }
        }
    }

    private static IReadOnlyList<IColumnInfo> GetRequiredColumns(
        GeneratorExecutionPlanStep step,
        GeneratorDataRequirement requirement,
        TableSnapshot source)
    {
        return requirement.Columns
            .Select(columnName => GetColumn(step.Id, source, columnName))
            .ToArray();
    }

    private static IColumnInfo GetColumn(string stepId, TableSnapshot table, string columnName)
    {
        return table.Columns.TryGetValue(columnName, out IColumnInfo? column)
            ? column
            : throw new InvalidOperationException(
                $"Generator step '{stepId}' references missing or unsupported text column " +
                $"{table.Table.SchemaName}.{table.Table.Name}.{columnName}.");
    }

    private static long? EstimateMaximumMemory(long rowCount, IReadOnlyList<IColumnInfo> columns)
    {
        if (columns.Any(column => column.MaxLength <= 0))
        {
            return null;
        }

        decimal bytesPerRow = columns.Sum(column =>
            (decimal)PerValueMemoryOverhead + column.MaxLength * (column.IsUnicodeText ? 2m : 1m));
        decimal total = bytesPerRow * rowCount;
        return total > long.MaxValue ? null : (long)total;
    }

    private static string TableKey(string schemaName, string tableName) => $"{schemaName}\u001f{tableName}";

    private sealed record TableSnapshot(
        ITableInfo Table,
        IReadOnlyDictionary<string, IColumnInfo> Columns);
}
