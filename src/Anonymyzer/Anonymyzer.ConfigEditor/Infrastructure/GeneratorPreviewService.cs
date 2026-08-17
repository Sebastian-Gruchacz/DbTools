namespace Anonymyzer.ConfigEditor.Infrastructure;

using Anonymyzer.Base.Generation;
using Anonymyzer.Configuration;

internal sealed class GeneratorPreviewService(GeneratorCatalog catalog)
{
    public async Task<IReadOnlyDictionary<string, string>> GenerateAsync(
        TableProcessingOptions table,
        IReadOnlyList<GeneratorProfileConfiguration> profiles,
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
                object configuration = generator.Configuration.Deserialize(profile.Options);
                IReadOnlyList<string> errors = generator.Configuration.Validate(configuration);
                if (errors.Count > 0)
                {
                    SetGroupMessage(samples, group, errors[0]);
                    continue;
                }

                var binding = new GeneratorBinding(
                    new GeneratorTableReference(table.SchemaName, table.TableName),
                    group.Bindings);
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
            if (generator?.Descriptor.Scope == GeneratorExecutionScope.Column)
            {
                samples[column.ColumnName] = "requires cloned data";
            }
        }

        return samples;
    }

    private static string FormatSample(object? value, string output)
    {
        return value?.ToString() ?? $"{output}: null";
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
