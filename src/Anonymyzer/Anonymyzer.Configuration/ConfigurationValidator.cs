namespace Anonymyzer.Configuration;

public static class ConfigurationValidator
{
    public static IReadOnlyList<string> Validate(AnonymizationConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var errors = new List<string>();
        if (!string.Equals(configuration.Version, AnonymizationConfiguration.CurrentVersion, StringComparison.Ordinal))
        {
            errors.Add($"Unsupported configuration version '{configuration.Version}'. Expected '{AnonymizationConfiguration.CurrentVersion}'.");
        }

        HashSet<string> profileIds = CollectUniqueIds(
            configuration.GeneratorProfiles.Select(profile => profile.Id),
            "generator profile",
            errors);
        Dictionary<string, GeneratorProfileConfiguration> profilesById = configuration.GeneratorProfiles
            .Where(profile => !string.IsNullOrWhiteSpace(profile.Id))
            .GroupBy(profile => profile.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        foreach (GeneratorProfileConfiguration profile in configuration.GeneratorProfiles)
        {
            if (string.IsNullOrWhiteSpace(profile.GeneratorType))
            {
                errors.Add($"Generator profile '{profile.Id}' has no generator type.");
            }

            if (string.IsNullOrWhiteSpace(profile.GeneratorVersion))
            {
                errors.Add($"Generator profile '{profile.Id}' has no generator version.");
            }
        }

        foreach (TableProcessingOptions table in configuration.Tables)
        {
            string tablePath = $"{table.SchemaName}.{table.TableName}";
            HashSet<string> columnNames = CollectUniqueIds(
                table.Columns.Select(column => column.ColumnName),
                $"column in {tablePath}",
                errors);
            HashSet<string> groupIds = CollectUniqueIds(
                table.GenerationGroups.Select(group => group.Id),
                $"generation group in {tablePath}",
                errors);

            foreach (ColumnProcessingOptions column in table.Columns)
            {
                if (!string.IsNullOrWhiteSpace(column.Generator.ProfileId)
                    && !profileIds.Contains(column.Generator.ProfileId))
                {
                    errors.Add($"Column {tablePath}.{column.ColumnName} references missing profile '{column.Generator.ProfileId}'.");
                }
                else if (!string.IsNullOrWhiteSpace(column.Generator.ProfileId)
                         && profilesById.TryGetValue(column.Generator.ProfileId, out GeneratorProfileConfiguration? columnProfile)
                         && (!columnProfile.GeneratorType.Equals(column.Generator.GeneratorType, StringComparison.OrdinalIgnoreCase)
                             || !columnProfile.GeneratorVersion.Equals(column.Generator.GeneratorVersion, StringComparison.Ordinal)))
                {
                    errors.Add($"Column {tablePath}.{column.ColumnName} does not match generator {columnProfile.GeneratorType} {columnProfile.GeneratorVersion} selected by profile '{columnProfile.Id}'.");
                }

                if (!string.IsNullOrWhiteSpace(column.GenerationGroupId)
                    && !groupIds.Contains(column.GenerationGroupId))
                {
                    errors.Add($"Column {tablePath}.{column.ColumnName} references missing group '{column.GenerationGroupId}'.");
                }
            }

            foreach (GenerationGroupConfiguration group in table.GenerationGroups)
            {
                if (string.IsNullOrWhiteSpace(group.GeneratorType) || string.IsNullOrWhiteSpace(group.GeneratorVersion))
                {
                    errors.Add($"Group {tablePath}.{group.Id} must select an exact generator type and version.");
                }

                if (!string.IsNullOrWhiteSpace(group.ProfileId) && !profileIds.Contains(group.ProfileId))
                {
                    errors.Add($"Group {tablePath}.{group.Id} references missing profile '{group.ProfileId}'.");
                }
                else if (!string.IsNullOrWhiteSpace(group.ProfileId)
                         && profilesById.TryGetValue(group.ProfileId, out GeneratorProfileConfiguration? groupProfile)
                         && (!groupProfile.GeneratorType.Equals(group.GeneratorType, StringComparison.OrdinalIgnoreCase)
                             || !groupProfile.GeneratorVersion.Equals(group.GeneratorVersion, StringComparison.Ordinal)))
                {
                    errors.Add($"Group {tablePath}.{group.Id} does not match generator {groupProfile.GeneratorType} {groupProfile.GeneratorVersion} selected by profile '{groupProfile.Id}'.");
                }

                foreach ((string output, string columnName) in group.Bindings)
                {
                    if (string.IsNullOrWhiteSpace(output))
                    {
                        errors.Add($"Group {tablePath}.{group.Id} contains an empty output name.");
                    }

                    if (!columnNames.Contains(columnName))
                    {
                        errors.Add($"Group {tablePath}.{group.Id} maps '{output}' to missing column '{columnName}'.");
                    }
                }
            }
        }

        return errors;
    }

    public static void EnsureValid(AnonymizationConfiguration configuration)
    {
        IReadOnlyList<string> errors = Validate(configuration);
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, errors));
        }
    }

    private static HashSet<string> CollectUniqueIds(
        IEnumerable<string> values,
        string itemDescription,
        ICollection<string> errors)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string value in values)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                errors.Add($"An empty {itemDescription} id is not allowed.");
            }
            else if (!result.Add(value))
            {
                errors.Add($"Duplicate {itemDescription} id '{value}'.");
            }
        }

        return result;
    }
}
