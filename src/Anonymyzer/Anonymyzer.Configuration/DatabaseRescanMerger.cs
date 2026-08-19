namespace Anonymyzer.Configuration;

public sealed class DatabaseRescanMerger
{
    public DatabaseRescanMergeResult Merge(
        AnonymizationConfiguration configuration,
        IEnumerable<TableProcessingOptions> freshTables)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(freshTables);

        TableProcessingOptions[] scanned = freshTables.ToArray();
        foreach (TableProcessingOptions table in configuration.Tables)
        {
            table.SchemaStatus = "Missing";
            foreach (ColumnProcessingOptions column in table.Columns)
            {
                column.SchemaStatus = "Missing";
            }
        }

        int addedTables = 0;
        int addedColumns = 0;
        int refreshedColumns = 0;
        int preservedSelections = 0;
        foreach (TableProcessingOptions freshTable in scanned)
        {
            TableProcessingOptions? existingTable = configuration.Tables.FirstOrDefault(table =>
                TableKey(table).Equals(TableKey(freshTable), StringComparison.OrdinalIgnoreCase));
            if (existingTable is null)
            {
                configuration.Tables.Add(freshTable);
                addedTables++;
                addedColumns += freshTable.Columns.Count;
                continue;
            }

            existingTable.SchemaStatus = "Current";
            foreach (ColumnProcessingOptions freshColumn in freshTable.Columns)
            {
                ColumnProcessingOptions? existingColumn = existingTable.Columns.FirstOrDefault(column =>
                    column.ColumnName.Equals(freshColumn.ColumnName, StringComparison.OrdinalIgnoreCase));
                if (existingColumn is null)
                {
                    existingTable.Columns.Add(freshColumn);
                    addedColumns++;
                    continue;
                }

                bool preserveSelection = HasProtectedSelection(existingColumn);
                existingColumn.Ordinal = freshColumn.Ordinal;
                existingColumn.DataType = freshColumn.DataType;
                existingColumn.MaxLength = freshColumn.MaxLength;
                existingColumn.Unicode = freshColumn.Unicode;
                existingColumn.SchemaStatus = "Current";
                existingColumn.Detection = freshColumn.Detection;
                if (preserveSelection)
                {
                    preservedSelections++;
                }
                else
                {
                    existingColumn.Generator = freshColumn.Generator;
                }

                refreshedColumns++;
            }

            existingTable.Columns = existingTable.Columns
                .OrderBy(column => column.SchemaStatus == "Missing" ? 1 : 0)
                .ThenBy(column => column.Ordinal)
                .ThenBy(column => column.ColumnName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        configuration.Tables = configuration.Tables
            .OrderBy(table => table.SchemaStatus == "Missing" ? 1 : 0)
            .ThenBy(table => table.SchemaName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(table => table.TableName, StringComparer.OrdinalIgnoreCase)
            .ToList();
        int missingTables = configuration.Tables.Count(table => table.SchemaStatus == "Missing");
        int missingColumns = configuration.Tables.Sum(table =>
            table.Columns.Count(column => column.SchemaStatus == "Missing"));
        return new DatabaseRescanMergeResult(
            addedTables,
            addedColumns,
            refreshedColumns,
            preservedSelections,
            missingTables,
            missingColumns);
    }

    private static bool HasProtectedSelection(ColumnProcessingOptions column) =>
        column.OperatorOverrides?.HasAny == true
        || column.Enabled
        || !string.IsNullOrWhiteSpace(column.SemanticRole)
        || !string.IsNullOrWhiteSpace(column.GenerationGroupId);

    private static string TableKey(TableProcessingOptions table) => $"{table.SchemaName}\u001f{table.TableName}";
}

public sealed record DatabaseRescanMergeResult(
    int AddedTables,
    int AddedColumns,
    int RefreshedColumns,
    int PreservedSelections,
    int MissingTables,
    int MissingColumns);
