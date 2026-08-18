namespace Anonymyzer.Generators.Simple;

using Anonymyzer.Base;
using Anonymyzer.Base.Generation;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public sealed class JsonPathRedactorGenerator : GeneratorBase<JsonPathRedactorGeneratorConfiguration>
{
    public const string GeneratorType = "JsonPathRedactor";
    public const string GeneratorVersion = "1.0.0";
    public const string ValueOutput = "Value";

    private static readonly GeneratorDescriptor GeneratorDescriptor = new(
        GeneratorType,
        GeneratorVersion,
        "JSON path redactor",
        GeneratorExecutionScope.Row,
        DbDataType.Text)
    {
        Outputs = [new GeneratorOutputDescriptor(ValueOutput, "JSON text", string.Empty, Required: true)]
    };

    private static readonly JsonPathRedactorGeneratorConfigurationCodec ConfigurationCodec = new();

    public override GeneratorDescriptor Descriptor => GeneratorDescriptor;

    public override IGeneratorConfigurationCodec Configuration => ConfigurationCodec;

    protected override IReadOnlyList<GeneratorDataRequirement> GetDataRequirements(
        GeneratorBinding binding,
        JsonPathRedactorGeneratorConfiguration configuration)
    {
        binding.GetRequiredOutput(ValueOutput);
        return Array.Empty<GeneratorDataRequirement>();
    }

    protected override ValueTask<IGeneratorSession> PrepareAsync(
        GeneratorPreparationContext context,
        JsonPathRedactorGeneratorConfiguration configuration,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<string> errors = Configuration.Validate(configuration);
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, errors));
        }

        Rule[] rules = configuration.Rules.Select(rule =>
        {
            JsonRedactionPath.TryParse(rule.Path, out JsonRedactionPath? path, out _);
            return new Rule(path!, JToken.Parse(rule.ReplacementJson));
        }).ToArray();
        return ValueTask.FromResult<IGeneratorSession>(new Session(
            context.Binding.GetRequiredOutput(ValueOutput),
            rules,
            configuration.RequireEveryPath));
    }

    private sealed class Session(string columnName, IReadOnlyList<Rule> rules, bool requireEveryPath)
        : IGeneratorSession
    {
        public ValueTask ApplyAsync(IGeneratorRow row, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            object? sourceValue = row.GetValue(columnName);
            if (sourceValue is null)
            {
                return ValueTask.CompletedTask;
            }

            if (sourceValue is not string sourceJson)
            {
                throw new InvalidOperationException(
                    $"JSON path redactor expected text in column '{columnName}', but received {sourceValue.GetType().Name}.");
            }

            JToken document;
            try
            {
                document = JToken.Parse(sourceJson);
            }
            catch (JsonReaderException exception)
            {
                throw new InvalidOperationException(
                    $"Column '{columnName}' contains invalid JSON. The source value was not included in this error.",
                    exception);
            }

            foreach (Rule rule in rules)
            {
                int replacements = Replace(ref document, rule.Path.Segments, 0, rule.Replacement);
                if (requireEveryPath && replacements == 0)
                {
                    throw new InvalidOperationException(
                        $"Required JSON path '{rule.Path.Value}' was not found in column '{columnName}'.");
                }
            }

            row.SetValue(columnName, document.ToString(Formatting.None));
            return ValueTask.CompletedTask;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        private static int Replace(
            ref JToken current,
            IReadOnlyList<JsonRedactionPath.Segment> segments,
            int segmentIndex,
            JToken replacement)
        {
            if (segmentIndex == segments.Count)
            {
                current = replacement.DeepClone();
                return 1;
            }

            JsonRedactionPath.Segment segment = segments[segmentIndex];
            if (segment.Kind == JsonRedactionPath.SegmentKind.Property)
            {
                if (current is not JObject objectValue
                    || objectValue.Property(segment.PropertyName, StringComparison.Ordinal) is not JProperty property)
                {
                    return 0;
                }

                JToken child = property.Value;
                int replacements = Replace(ref child, segments, segmentIndex + 1, replacement);
                property.Value = child;
                return replacements;
            }

            if (current is not JArray array)
            {
                return 0;
            }

            int count = 0;
            for (int index = 0; index < array.Count; index++)
            {
                JToken child = array[index];
                count += Replace(ref child, segments, segmentIndex + 1, replacement);
                array[index] = child;
            }

            return count;
        }
    }

    private sealed record Rule(JsonRedactionPath Path, JToken Replacement);
}
