namespace Anonymyzer.Console.Planning;

using Anonymyzer.Base.Generation;
using Anonymyzer.Configuration;
using Newtonsoft.Json.Linq;

internal sealed class AnonymizationExecutionPlanner
{
    public const int DefaultBatchSize = 1000;

    private readonly IReadOnlyDictionary<string, IGenerator> _generators;

    public AnonymizationExecutionPlanner(IEnumerable<IGenerator> generators)
    {
        ArgumentNullException.ThrowIfNull(generators);
        _generators = generators.ToDictionary(
            generator => GeneratorKey(generator.Descriptor.Type, generator.Descriptor.Version),
            StringComparer.OrdinalIgnoreCase);
    }

    public AnonymizationExecutionPlan Build(
        AnonymizationConfiguration configuration,
        int batchSize = DefaultBatchSize)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        if (batchSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(batchSize), "Batch size must be greater than zero.");
        }

        Dictionary<string, GeneratorProfileConfiguration> profiles = configuration.GeneratorProfiles
            .ToDictionary(profile => profile.Id, StringComparer.OrdinalIgnoreCase);
        ValidateProfiles(configuration.GeneratorProfiles);
        var steps = new List<GeneratorExecutionPlanStep>();

        foreach (TableProcessingOptions table in configuration.Tables.Where(table => table.Enabled))
        {
            BuildGroupSteps(table, profiles, steps, batchSize);
            BuildColumnSteps(table, profiles, steps, batchSize);
        }

        EnsureAllEnabledColumnsArePlanned(configuration, steps);
        return new AnonymizationExecutionPlan(batchSize, OrderByGeneratedDependencies(steps));
    }

    private void BuildGroupSteps(
        TableProcessingOptions table,
        IReadOnlyDictionary<string, GeneratorProfileConfiguration> profiles,
        ICollection<GeneratorExecutionPlanStep> steps,
        int batchSize)
    {
        foreach (GenerationGroupConfiguration group in table.GenerationGroups)
        {
            Dictionary<string, string> enabledBindings = group.Bindings
                .Where(binding => IsEnabledColumnBoundToGroup(table, binding.Value, group.Id))
                .ToDictionary(binding => binding.Key, binding => binding.Value, StringComparer.OrdinalIgnoreCase);
            if (enabledBindings.Count == 0)
            {
                continue;
            }

            GeneratorProfileConfiguration profile = GetProfile(profiles, group.ProfileId);
            IGenerator generator = GetGenerator(group.GeneratorType, group.GeneratorVersion);
            EnsureProfileMatches(profile, generator.Descriptor, $"group '{group.Id}'");
            EnsureKnownOutputs(generator.Descriptor, enabledBindings, $"group '{group.Id}'");
            EnsureRequiredOutputs(generator.Descriptor, enabledBindings, $"group '{group.Id}'");
            EnsureSupportedColumns(table, generator.Descriptor, enabledBindings.Values);
            AddStep(
                steps,
                $"{TablePath(table)}/group:{group.Id}",
                table,
                generator,
                enabledBindings,
                profile.Options,
                batchSize);
        }
    }

    private void BuildColumnSteps(
        TableProcessingOptions table,
        IReadOnlyDictionary<string, GeneratorProfileConfiguration> profiles,
        ICollection<GeneratorExecutionPlanStep> steps,
        int batchSize)
    {
        foreach (ColumnProcessingOptions column in table.Columns
                     .Where(column => column.Enabled && string.IsNullOrWhiteSpace(column.GenerationGroupId))
                     .OrderBy(column => column.Ordinal))
        {
            GeneratorProfileConfiguration profile = GetProfile(profiles, column.Generator.ProfileId);
            IGenerator generator = GetGenerator(column.Generator.GeneratorType, column.Generator.GeneratorVersion);
            EnsureProfileMatches(profile, generator.Descriptor, $"column '{column.ColumnName}'");
            EnsureSupportedColumn(column, generator.Descriptor);
            if (generator.Descriptor.Outputs.Count != 1)
            {
                throw new InvalidOperationException(
                    $"Generator {generator.Descriptor.Type} {generator.Descriptor.Version} has multiple outputs and must be used through a generation group.");
            }

            var bindings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [generator.Descriptor.Outputs[0].Name] = column.ColumnName
            };
            JObject options = (JObject)profile.Options.DeepClone();
            options.Merge(column.Generator.Options, new JsonMergeSettings
            {
                MergeArrayHandling = MergeArrayHandling.Replace,
                MergeNullValueHandling = MergeNullValueHandling.Merge
            });
            AddStep(
                steps,
                $"{TablePath(table)}/column:{column.ColumnName}",
                table,
                generator,
                bindings,
                options,
                batchSize);
        }
    }

    private void AddStep(
        ICollection<GeneratorExecutionPlanStep> steps,
        string id,
        TableProcessingOptions table,
        IGenerator generator,
        IReadOnlyDictionary<string, string> outputs,
        JObject options,
        int batchSize)
    {
        object generatorConfiguration = generator.Configuration.Deserialize(options);
        IReadOnlyList<string> errors = generator.Configuration.Validate(generatorConfiguration);
        if (errors.Count > 0)
        {
            throw new InvalidOperationException($"Generator step '{id}' is invalid: {string.Join("; ", errors)}");
        }

        var binding = new GeneratorBinding(
            new GeneratorTableReference(table.SchemaName, table.TableName),
            outputs,
            outputs.ToDictionary(
                output => output.Key,
                output => ParseDataType(table, output.Value),
                StringComparer.OrdinalIgnoreCase));
        steps.Add(new GeneratorExecutionPlanStep(
            id,
            binding.Table,
            generator.Descriptor,
            binding,
            generatorConfiguration,
            generator.GetDataRequirements(binding, generatorConfiguration),
            batchSize));
    }

    private static Anonymyzer.Base.DbDataType ParseDataType(TableProcessingOptions table, string columnName)
    {
        string configuredType = table.Columns.Single(column =>
            column.ColumnName.Equals(columnName, StringComparison.OrdinalIgnoreCase)).DataType;
        return Enum.TryParse(configuredType, ignoreCase: true, out Anonymyzer.Base.DbDataType dataType)
            ? dataType
            : Anonymyzer.Base.DbDataType.Other;
    }

    private static IReadOnlyList<GeneratorExecutionPlanStep> OrderByGeneratedDependencies(
        IReadOnlyList<GeneratorExecutionPlanStep> steps)
    {
        Dictionary<string, GeneratorExecutionPlanStep> producers = BuildProducerIndex(steps);
        Dictionary<GeneratorExecutionPlanStep, HashSet<GeneratorExecutionPlanStep>> dependencies = steps
            .ToDictionary(step => step, _ => new HashSet<GeneratorExecutionPlanStep>());

        foreach (GeneratorExecutionPlanStep step in steps)
        {
            foreach (GeneratorDataRequirement requirement in step.DataRequirements
                         .Where(requirement => requirement.ValueSource == GeneratorValueSource.Generated))
            {
                foreach (string column in requirement.Columns)
                {
                    string key = ColumnKey(requirement.Table, column);
                    if (!producers.TryGetValue(key, out GeneratorExecutionPlanStep? producer))
                    {
                        throw new InvalidOperationException(
                            $"Generator step '{step.Id}' requires generated value {TablePath(requirement.Table)}.{column}, but no active step produces it.");
                    }

                    dependencies[step].Add(producer);
                }
            }
        }

        var ordered = new List<GeneratorExecutionPlanStep>(steps.Count);
        var remaining = new HashSet<GeneratorExecutionPlanStep>(steps);
        while (remaining.Count > 0)
        {
            GeneratorExecutionPlanStep? next = steps.FirstOrDefault(step =>
                remaining.Contains(step) && dependencies[step].All(dependency => !remaining.Contains(dependency)));
            if (next is null)
            {
                string cycle = string.Join(", ", steps.Where(remaining.Contains).Select(step => step.Id));
                throw new InvalidOperationException($"Generated-value dependency cycle detected: {cycle}.");
            }

            ordered.Add(next);
            remaining.Remove(next);
        }

        return ordered;
    }

    private void ValidateProfiles(IEnumerable<GeneratorProfileConfiguration> profiles)
    {
        foreach (GeneratorProfileConfiguration profile in profiles)
        {
            IGenerator generator = GetGenerator(profile.GeneratorType, profile.GeneratorVersion);
            object configuration = generator.Configuration.Deserialize(profile.Options);
            IReadOnlyList<string> errors = generator.Configuration.Validate(configuration);
            if (errors.Count > 0)
            {
                throw new InvalidOperationException(
                    $"Generator profile '{profile.Id}' is invalid: {string.Join("; ", errors)}");
            }
        }
    }

    private static void EnsureAllEnabledColumnsArePlanned(
        AnonymizationConfiguration configuration,
        IReadOnlyCollection<GeneratorExecutionPlanStep> steps)
    {
        var plannedColumns = new HashSet<string>(
            steps.SelectMany(step => step.Binding.Outputs.Values.Select(column => ColumnKey(step.TargetTable, column))),
            StringComparer.OrdinalIgnoreCase);

        foreach (TableProcessingOptions table in configuration.Tables.Where(table => table.Enabled))
        {
            var tableReference = new GeneratorTableReference(table.SchemaName, table.TableName);
            ColumnProcessingOptions? missing = table.Columns.FirstOrDefault(column =>
                column.Enabled && !plannedColumns.Contains(ColumnKey(tableReference, column.ColumnName)));
            if (missing is not null)
            {
                throw new InvalidOperationException(
                    $"Enabled column {TablePath(table)}.{missing.ColumnName} has no active generator step.");
            }
        }
    }

    private static Dictionary<string, GeneratorExecutionPlanStep> BuildProducerIndex(
        IReadOnlyList<GeneratorExecutionPlanStep> steps)
    {
        var result = new Dictionary<string, GeneratorExecutionPlanStep>(StringComparer.OrdinalIgnoreCase);
        foreach (GeneratorExecutionPlanStep step in steps)
        {
            foreach (string column in step.Binding.Outputs.Values)
            {
                string key = ColumnKey(step.TargetTable, column);
                if (!result.TryAdd(key, step))
                {
                    throw new InvalidOperationException(
                        $"More than one active generator step produces {TablePath(step.TargetTable)}.{column}.");
                }
            }
        }

        return result;
    }

    private GeneratorProfileConfiguration GetProfile(
        IReadOnlyDictionary<string, GeneratorProfileConfiguration> profiles,
        string profileId)
    {
        return profiles.TryGetValue(profileId, out GeneratorProfileConfiguration? profile)
            ? profile
            : throw new InvalidOperationException($"Generator profile '{profileId}' is not configured.");
    }

    private IGenerator GetGenerator(string type, string version)
    {
        return _generators.TryGetValue(GeneratorKey(type, version), out IGenerator? generator)
            ? generator
            : throw new InvalidOperationException($"Generator {type} {version} is not installed.");
    }

    private static void EnsureProfileMatches(
        GeneratorProfileConfiguration profile,
        GeneratorDescriptor descriptor,
        string target)
    {
        if (!profile.GeneratorType.Equals(descriptor.Type, StringComparison.OrdinalIgnoreCase)
            || !profile.GeneratorVersion.Equals(descriptor.Version, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Profile '{profile.Id}' does not match generator {descriptor.Type} {descriptor.Version} selected by {target}.");
        }
    }

    private static void EnsureRequiredOutputs(
        GeneratorDescriptor descriptor,
        IReadOnlyDictionary<string, string> bindings,
        string target)
    {
        GeneratorOutputDescriptor? missing = descriptor.Outputs.FirstOrDefault(output =>
            output.Required && !bindings.ContainsKey(output.Name));
        if (missing is not null)
        {
            throw new InvalidOperationException(
                $"Required output '{missing.Name}' of {descriptor.Type} is disabled in {target}.");
        }
    }

    private static void EnsureKnownOutputs(
        GeneratorDescriptor descriptor,
        IReadOnlyDictionary<string, string> bindings,
        string target)
    {
        string? unknown = bindings.Keys.FirstOrDefault(outputName =>
            descriptor.Outputs.All(output => !output.Name.Equals(outputName, StringComparison.OrdinalIgnoreCase)));
        if (unknown is not null)
        {
            throw new InvalidOperationException(
                $"Unknown output '{unknown}' of {descriptor.Type} is bound in {target}.");
        }
    }

    private static void EnsureSupportedColumns(
        TableProcessingOptions table,
        GeneratorDescriptor descriptor,
        IEnumerable<string> columnNames)
    {
        foreach (string columnName in columnNames)
        {
            ColumnProcessingOptions column = table.Columns.Single(candidate =>
                candidate.ColumnName.Equals(columnName, StringComparison.OrdinalIgnoreCase));
            EnsureSupportedColumn(column, descriptor);
        }
    }

    private static void EnsureSupportedColumn(
        ColumnProcessingOptions column,
        GeneratorDescriptor descriptor)
    {
        if (!string.IsNullOrWhiteSpace(column.DataType)
            && descriptor.SupportedDataTypes.All(dataType =>
                !column.DataType.Equals(dataType.ToString(), StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException(
                $"Generator {descriptor.Type} supports {string.Join(" or ", descriptor.SupportedDataTypes)}, "
                + $"but column '{column.ColumnName}' is {column.DataType}.");
        }
    }

    private static bool IsEnabledColumnBoundToGroup(
        TableProcessingOptions table,
        string columnName,
        string groupId)
    {
        return table.Columns.Any(column =>
            column.Enabled
            && column.ColumnName.Equals(columnName, StringComparison.OrdinalIgnoreCase)
            && column.GenerationGroupId.Equals(groupId, StringComparison.OrdinalIgnoreCase));
    }

    private static string GeneratorKey(string type, string version) => $"{type}\u001f{version}";

    private static string ColumnKey(GeneratorTableReference table, string column) =>
        $"{table.SchemaName}\u001f{table.TableName}\u001f{column}";

    private static string TablePath(TableProcessingOptions table) => $"{table.SchemaName}.{table.TableName}";

    private static string TablePath(GeneratorTableReference table) => $"{table.SchemaName}.{table.TableName}";
}
