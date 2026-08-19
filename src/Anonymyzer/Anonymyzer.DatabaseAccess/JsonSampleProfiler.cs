namespace Anonymyzer.DatabaseAccess;

using System.Text.Json;

public sealed class JsonSampleProfiler
{
    public const int MaximumDepth = 16;
    public const int MaximumDistinctPaths = 200;
    public const int MaximumVisitedValuesPerSample = 10_000;

    public JsonSampleProfile Profile(IEnumerable<ColumnSample> samples)
    {
        ArgumentNullException.ThrowIfNull(samples);
        ColumnSample[] materialized = samples.ToArray();
        var paths = new Dictionary<string, MutablePathProfile>(StringComparer.Ordinal);
        int validSamples = 0;
        int invalidSamples = 0;
        int truncatedSamples = 0;
        bool wasLimitReached = false;

        for (int sampleIndex = 0; sampleIndex < materialized.Length; sampleIndex++)
        {
            ColumnSample sample = materialized[sampleIndex];
            if (sample.WasTruncated)
            {
                truncatedSamples++;
                continue;
            }

            try
            {
                using JsonDocument document = JsonDocument.Parse(sample.Value);
                validSamples++;
                int visitedValues = 0;
                Visit(
                    document.RootElement,
                    "$",
                    sampleIndex,
                    depth: 0,
                    paths,
                    ref visitedValues,
                    ref wasLimitReached);
            }
            catch (JsonException)
            {
                invalidSamples++;
            }
        }

        JsonPathProfile[] pathProfiles = paths
            .OrderBy(pair => pair.Key == "$" ? 0 : 1)
            .ThenBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => pair.Value.Create(pair.Key))
            .ToArray();
        return new JsonSampleProfile(
            materialized.Length,
            validSamples,
            invalidSamples,
            truncatedSamples,
            wasLimitReached,
            pathProfiles);
    }

    private static void Visit(
        JsonElement value,
        string path,
        int sampleIndex,
        int depth,
        IDictionary<string, MutablePathProfile> paths,
        ref int visitedValues,
        ref bool wasLimitReached)
    {
        if (visitedValues >= MaximumVisitedValuesPerSample)
        {
            wasLimitReached = true;
            return;
        }

        visitedValues++;
        if (!paths.TryGetValue(path, out MutablePathProfile? profile))
        {
            if (paths.Count >= MaximumDistinctPaths)
            {
                wasLimitReached = true;
                return;
            }

            profile = new MutablePathProfile();
            paths.Add(path, profile);
        }

        profile.Add(sampleIndex, value.ValueKind);
        if (depth >= MaximumDepth)
        {
            if (value.ValueKind is JsonValueKind.Array or JsonValueKind.Object)
            {
                wasLimitReached = true;
            }

            return;
        }

        if (value.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty property in value.EnumerateObject())
            {
                Visit(
                    property.Value,
                    AppendProperty(path, property.Name),
                    sampleIndex,
                    depth + 1,
                    paths,
                    ref visitedValues,
                    ref wasLimitReached);
            }
        }
        else if (value.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in value.EnumerateArray())
            {
                Visit(
                    item,
                    path + "[]",
                    sampleIndex,
                    depth + 1,
                    paths,
                    ref visitedValues,
                    ref wasLimitReached);
            }
        }
    }

    private static string AppendProperty(string path, string propertyName) =>
        $"{path}/{propertyName.Replace("~", "~0", StringComparison.Ordinal).Replace("/", "~1", StringComparison.Ordinal)}";

    private sealed class MutablePathProfile
    {
        private readonly HashSet<int> _sampleIndexes = new();
        private readonly HashSet<string> _valueKinds = new(StringComparer.Ordinal);

        public int ValueCount { get; private set; }

        public void Add(int sampleIndex, JsonValueKind valueKind)
        {
            _sampleIndexes.Add(sampleIndex);
            _valueKinds.Add(valueKind is JsonValueKind.True or JsonValueKind.False
                ? "Boolean"
                : valueKind.ToString());
            ValueCount++;
        }

        public JsonPathProfile Create(string path) =>
            new(
                path,
                _sampleIndexes.Count,
                ValueCount,
                _valueKinds.OrderBy(kind => kind, StringComparer.Ordinal).ToArray());
    }
}

public sealed record JsonSampleProfile(
    int TotalSamples,
    int ValidSamples,
    int InvalidSamples,
    int TruncatedSamples,
    bool WasLimitReached,
    IReadOnlyList<JsonPathProfile> Paths)
{
    public string Summary
    {
        get
        {
            if (TotalSamples == 0)
            {
                return "JSON profile: load samples to analyze.";
            }

            int completeSamples = TotalSamples - TruncatedSamples;
            string limit = WasLimitReached ? " Profile limit reached." : string.Empty;
            if (ValidSamples == 0)
            {
                return $"JSON profile: no complete JSON documents; {InvalidSamples} invalid, " +
                       $"{TruncatedSamples} truncated.{limit}";
            }

            return $"JSON profile: {ValidSamples}/{completeSamples} complete sample(s) valid, " +
                   $"{Paths.Count} path(s), {TruncatedSamples} truncated.{limit}";
        }
    }
}

public sealed record JsonPathProfile(
    string Path,
    int DocumentCount,
    int ValueCount,
    IReadOnlyList<string> ValueKinds)
{
    public string ValueKindsDisplay => string.Join(", ", ValueKinds);
}
