namespace Anonymyzer.ConfigEditor.Infrastructure;

using Anonymyzer.Base.Generation;
using Anonymyzer.Configuration;
using Anonymyzer.DatabaseAccess;
using Newtonsoft.Json.Linq;

internal sealed class GeneratorPreviewService(GeneratorCatalog catalog)
{
    public async Task<IReadOnlyDictionary<string, string>> GenerateAsync(
        AnonymizationConfiguration anonymizationConfiguration,
        TableProcessingOptions table,
        IReadOnlyList<GeneratorProfileConfiguration> profiles,
        string? connectionEnvironmentVariable = null,
        int maximumRows = 10,
        CancellationToken cancellationToken = default)
    {
        var samples = table.Columns.ToDictionary(column => column.ColumnName, _ => "—", StringComparer.OrdinalIgnoreCase);
        var profilesById = profiles
            .Where(profile => !string.IsNullOrWhiteSpace(profile.Id))
            .GroupBy(profile => profile.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        foreach (GenerationGroupConfiguration group in table.GenerationGroups)
        {
            if (!profilesById.TryGetValue(group.ProfileId, out GeneratorProfileConfiguration? profile))
            {
                SetGroupMessage(samples, group, "missing profile");
                continue;
            }

            IGenerator? generator = catalog.Find(profile.GeneratorType, profile.GeneratorVersion);
            if (generator is null)
            {
                SetGroupMessage(samples, group, "generator unavailable");
                continue;
            }

            if (generator.Descriptor.Scope != GeneratorExecutionScope.Row)
            {
                SetGroupMessage(samples, group, "requires cloned data");
                continue;
            }

            try
            {
                object configuration = generator.Configuration.Deserialize(
                    GeneratorOptionsResolver.ResolveGroupOptions(profile, group));
                IReadOnlyList<string> errors = generator.Configuration.Validate(configuration);
                if (errors.Count > 0)
                {
                    SetGroupMessage(samples, group, errors[0]);
                    continue;
                }

                var binding = new GeneratorBinding(
                    new GeneratorTableReference(table.SchemaName, table.TableName),
                    group.Bindings);
                if (generator.Descriptor.RequiresExistingValue)
                {
                    if (group.Bindings.Count != 1 || generator.Descriptor.Outputs.Count != 1)
                    {
                        SetGroupMessage(samples, group, "existing-value preview supports one output");
                        continue;
                    }

                    string columnName = group.Bindings.Values.Single();
                    ColumnProcessingOptions column = table.Columns.Single(candidate =>
                        candidate.ColumnName.Equals(columnName, StringComparison.OrdinalIgnoreCase));
                    samples[columnName] = await GenerateExistingValuePreviewAsync(
                        anonymizationConfiguration,
                        table,
                        column,
                        generator,
                        configuration,
                        binding,
                        generator.Descriptor.Outputs.Single(),
                        connectionEnvironmentVariable,
                        maximumRows,
                        cancellationToken);
                    continue;
                }

                await using IGeneratorSession session = await generator.PrepareAsync(
                    new GeneratorPreparationContext(binding, new RejectingDataReader()),
                    configuration,
                    cancellationToken);
                var row = new PreviewRow();
                await session.ApplyAsync(row, cancellationToken);

                foreach ((string output, string columnName) in group.Bindings)
                {
                    samples[columnName] = FormatSample(row.GetBoundValue(columnName), output);
                }
            }
            catch (Exception exception)
            {
                SetGroupMessage(samples, group, exception.Message);
            }
        }

        foreach (ColumnProcessingOptions column in table.Columns.Where(column => string.IsNullOrWhiteSpace(column.GenerationGroupId)))
        {
            if (string.IsNullOrWhiteSpace(column.Generator.ProfileId)
                || !profilesById.TryGetValue(column.Generator.ProfileId, out GeneratorProfileConfiguration? profile))
            {
                continue;
            }

            IGenerator? generator = catalog.Find(profile.GeneratorType, profile.GeneratorVersion);
            if (generator is null)
            {
                samples[column.ColumnName] = "generator unavailable";
                continue;
            }

            if (generator.Descriptor.Outputs.Count != 1)
            {
                samples[column.ColumnName] = "requires generation group";
                continue;
            }

            try
            {
                JObject options = (JObject)profile.Options.DeepClone();
                options.Merge(column.Generator.Options, new JsonMergeSettings
                {
                    MergeArrayHandling = MergeArrayHandling.Replace,
                    MergeNullValueHandling = MergeNullValueHandling.Merge
                });
                object configuration = generator.Configuration.Deserialize(options);
                IReadOnlyList<string> errors = generator.Configuration.Validate(configuration);
                if (errors.Count > 0)
                {
                    samples[column.ColumnName] = errors[0];
                    continue;
                }

                GeneratorOutputDescriptor output = generator.Descriptor.Outputs.Single();
                var binding = new GeneratorBinding(
                    new GeneratorTableReference(table.SchemaName, table.TableName),
                    new Dictionary<string, string> { [output.Name] = column.ColumnName });
                IReadOnlyList<GeneratorDataRequirement> requirements =
                    generator.GetDataRequirements(binding, configuration);
                if (generator.Descriptor.Scope == GeneratorExecutionScope.Relational)
                {
                    samples[column.ColumnName] = await GenerateRelationalPreviewAsync(
                        anonymizationConfiguration,
                        generator,
                        configuration,
                        binding,
                        requirements,
                        output,
                        connectionEnvironmentVariable,
                        maximumRows,
                        cancellationToken);
                    continue;
                }

                if (generator.Descriptor.Scope == GeneratorExecutionScope.Column)
                {
                    samples[column.ColumnName] = await GenerateColumnPreviewAsync(
                        anonymizationConfiguration,
                        generator,
                        configuration,
                        binding,
                        requirements,
                        output,
                        connectionEnvironmentVariable,
                        maximumRows,
                        cancellationToken);
                    continue;
                }

                if (generator.Descriptor.RequiresExistingValue)
                {
                    samples[column.ColumnName] = await GenerateExistingValuePreviewAsync(
                        anonymizationConfiguration,
                        table,
                        column,
                        generator,
                        configuration,
                        binding,
                        output,
                        connectionEnvironmentVariable,
                        maximumRows,
                        cancellationToken);
                    continue;
                }

                await using IGeneratorSession session = await generator.PrepareAsync(
                    new GeneratorPreparationContext(binding, new RejectingDataReader()),
                    configuration,
                    cancellationToken);
                var row = new PreviewRow();
                row.SetValue(column.ColumnName, "preview-source");
                foreach (GeneratorDataRequirement requirement in requirements.Where(requirement =>
                             !requirement.RequiresCompleteScan
                             && requirement.Table == binding.Table))
                {
                    foreach (string sourceColumn in requirement.Columns)
                    {
                        row.SetValue(sourceColumn, PreviewSourceValue(sourceColumn));
                    }
                }

                await session.ApplyAsync(row, cancellationToken);
                samples[column.ColumnName] = FormatSample(row.GetBoundValue(column.ColumnName), output.Name);
            }
            catch (Exception exception)
            {
                samples[column.ColumnName] = exception.Message;
            }
        }

        return samples;
    }

    public bool RequiresCloneData(
        TableProcessingOptions table,
        IReadOnlyList<GeneratorProfileConfiguration> profiles)
    {
        var profilesById = profiles
            .Where(profile => !string.IsNullOrWhiteSpace(profile.Id))
            .GroupBy(profile => profile.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        IEnumerable<string> profileIds = table.Columns
            .Where(column => string.IsNullOrWhiteSpace(column.GenerationGroupId))
            .Select(column => column.Generator.ProfileId)
            .Concat(table.GenerationGroups.Select(group => group.ProfileId));
        return profileIds.Any(profileId =>
            profilesById.TryGetValue(profileId, out GeneratorProfileConfiguration? profile)
            && catalog.Find(profile.GeneratorType, profile.GeneratorVersion)?.Descriptor is { } descriptor
            && (descriptor.Scope is GeneratorExecutionScope.Column or GeneratorExecutionScope.Relational
                || descriptor.RequiresExistingValue));
    }

    private static async Task<string> GenerateRelationalPreviewAsync(
        AnonymizationConfiguration anonymizationConfiguration,
        IGenerator generator,
        object generatorConfiguration,
        GeneratorBinding binding,
        IReadOnlyList<GeneratorDataRequirement> requirements,
        GeneratorOutputDescriptor output,
        string? connectionEnvironmentVariable,
        int maximumRows,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(connectionEnvironmentVariable))
        {
            return "requires cloned data";
        }

        GeneratorDataRequirement? targetReference = requirements.SingleOrDefault(requirement =>
            requirement.Table == binding.Table
            && !requirement.RequiresCompleteScan
            && requirement.Columns.Count == 1);
        GeneratorDataRequirement? lookup = requirements.SingleOrDefault(requirement =>
            requirement.Table != binding.Table
            && requirement.RequiresCompleteScan
            && requirement.ValueSource == GeneratorValueSource.Original
            && requirement.Columns.Count == 1);
        if (targetReference is null || lookup is null)
        {
            return "relational preview not supported";
        }

        var reader = new LimitedGeneratorPreviewDataReader(
            anonymizationConfiguration,
            connectionEnvironmentVariable,
            maximumRows);
        await using IGeneratorSession session = await generator.PrepareAsync(
            new GeneratorPreparationContext(binding, reader),
            generatorConfiguration,
            cancellationToken);
        if (reader.LoadedRows.Count == 0)
        {
            return "no rows in lookup sample";
        }

        string targetColumn = targetReference.Columns[0];
        string lookupColumn = lookup.Columns[0];
        var generatedValues = new List<string>(reader.LoadedRows.Count);
        foreach (GeneratorDataRow lookupRow in reader.LoadedRows)
        {
            var row = new PreviewRow();
            row.SetValue(targetColumn, lookupRow.GetValue(lookupColumn));
            await session.ApplyAsync(row, cancellationToken);
            generatedValues.Add(FormatSample(row.GetBoundValue(binding.GetRequiredOutput(output.Name)), output.Name));
        }

        string displayedValues = string.Join(" | ", generatedValues.Take(3));
        if (generatedValues.Count > 3)
        {
            displayedValues += " | …";
        }

        return $"{displayedValues} [{generatedValues.Count}-row lookup sample]";
    }

    private static async Task<string> GenerateExistingValuePreviewAsync(
        AnonymizationConfiguration anonymizationConfiguration,
        TableProcessingOptions table,
        ColumnProcessingOptions column,
        IGenerator generator,
        object generatorConfiguration,
        GeneratorBinding binding,
        GeneratorOutputDescriptor output,
        string? connectionEnvironmentVariable,
        int maximumRows,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(connectionEnvironmentVariable))
        {
            return "requires cloned data";
        }

        IReadOnlyList<ColumnSample> loaded = await new ColumnSampleReader().ReadAsync(
            anonymizationConfiguration,
            table,
            column,
            connectionEnvironmentVariable,
            maximumRows,
            cancellationToken);
        ColumnSample[] complete = loaded.Where(sample => !sample.WasTruncated).ToArray();
        if (complete.Length == 0)
        {
            return loaded.Count == 0 ? "no non-null rows in clone sample" : "clone sample values exceed preview limit";
        }

        await using IGeneratorSession session = await generator.PrepareAsync(
            new GeneratorPreparationContext(binding, new RejectingDataReader()),
            generatorConfiguration,
            cancellationToken);
        var generatedValues = new List<string>(complete.Length);
        foreach (ColumnSample sample in complete)
        {
            var row = new PreviewRow();
            row.SetValue(column.ColumnName, sample.Value);
            await session.ApplyAsync(row, cancellationToken);
            generatedValues.Add(FormatSample(row.GetBoundValue(column.ColumnName), output.Name));
        }

        string displayedValues = string.Join(" | ", generatedValues.Take(3));
        if (generatedValues.Count > 3)
        {
            displayedValues += " | …";
        }

        return $"{displayedValues} [{generatedValues.Count}-row clone sample]";
    }

    private static async Task<string> GenerateColumnPreviewAsync(
        AnonymizationConfiguration anonymizationConfiguration,
        IGenerator generator,
        object generatorConfiguration,
        GeneratorBinding binding,
        IReadOnlyList<GeneratorDataRequirement> requirements,
        GeneratorOutputDescriptor output,
        string? connectionEnvironmentVariable,
        int maximumRows,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(connectionEnvironmentVariable))
        {
            return "requires cloned data";
        }

        if (requirements.Count != 1
            || !requirements[0].RequiresCompleteScan)
        {
            return "column preview not supported";
        }

        var reader = new LimitedGeneratorPreviewDataReader(
            anonymizationConfiguration,
            connectionEnvironmentVariable,
            maximumRows);
        await using IGeneratorSession session = await generator.PrepareAsync(
            new GeneratorPreparationContext(binding, reader),
            generatorConfiguration,
            cancellationToken);
        if (reader.LoadedRows.Count == 0)
        {
            return "no rows in clone sample";
        }

        GeneratorDataRequirement requirement = requirements[0];
        var generatedValues = new List<string>(reader.LoadedRows.Count);
        foreach (GeneratorDataRow sourceRow in reader.LoadedRows)
        {
            var row = new PreviewRow();
            foreach (string columnName in requirement.Columns)
            {
                row.SetValue(columnName, sourceRow.GetValue(columnName));
            }

            await session.ApplyAsync(row, cancellationToken);
            generatedValues.Add(FormatSample(row.GetBoundValue(binding.GetRequiredOutput(output.Name)), output.Name));
        }

        string displayedValues = string.Join(" | ", generatedValues.Take(3));
        if (generatedValues.Count > 3)
        {
            displayedValues += " | …";
        }

        return $"{displayedValues} [{generatedValues.Count}-row clone sample]";
    }

    private static string FormatSample(object? value, string output)
    {
        return value?.ToString() ?? $"{output}: null";
    }

    private static string PreviewSourceValue(string columnName)
    {
        if (columnName.Contains("birth", StringComparison.OrdinalIgnoreCase)
            || columnName.Contains("date", StringComparison.OrdinalIgnoreCase)
            || columnName.Contains("urodz", StringComparison.OrdinalIgnoreCase))
        {
            return "1985-04-12";
        }

        if (columnName.Contains("gender", StringComparison.OrdinalIgnoreCase)
            || columnName.Contains("sex", StringComparison.OrdinalIgnoreCase)
            || columnName.Contains("plec", StringComparison.OrdinalIgnoreCase)
            || columnName.Contains("płeć", StringComparison.OrdinalIgnoreCase))
        {
            return "Female";
        }

        return columnName.Contains("last", StringComparison.OrdinalIgnoreCase)
            || columnName.Contains("surname", StringComparison.OrdinalIgnoreCase)
            || columnName.Contains("nazw", StringComparison.OrdinalIgnoreCase)
                ? "Kowalski"
                : "Jan";
    }

    private static void SetGroupMessage(
        IDictionary<string, string> samples,
        GenerationGroupConfiguration group,
        string message)
    {
        foreach (string columnName in group.Bindings.Values)
        {
            samples[columnName] = message;
        }
    }

    private sealed class RejectingDataReader : IGeneratorDataReader
    {
        public IAsyncEnumerable<GeneratorDataRow> ReadAsync(
            GeneratorDataRequirement requirement,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Preview for this generator requires data from the detached clone.");
        }
    }

    private sealed class PreviewRow : IGeneratorRow
    {
        private readonly Dictionary<string, object?> _values = new(StringComparer.OrdinalIgnoreCase);

        public object? GetValue(string columnName)
        {
            return _values.TryGetValue(columnName, out object? value) ? value : null;
        }

        public object? GetBoundValue(string columnName) => GetValue(columnName);

        public void SetValue(string columnName, object? value)
        {
            _values[columnName] = value;
        }
    }
}
