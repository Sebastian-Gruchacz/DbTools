namespace Anonymyzer.PostgreSql.Tests;

using Anonymyzer.DatabaseAccess;

public sealed class JsonSampleProfilerTests
{
    [Fact]
    public void ProfilesStableOptionalAndRepeatedPaths()
    {
        ColumnSample[] samples =
        [
            new(1, "{\"name\":\"Ada\",\"tags\":[\"math\",\"code\"],\"address\":{\"city\":\"London\"}}", false),
            new(2, "{\"name\":null,\"tags\":[],\"extra\":42}", false)
        ];

        JsonSampleProfile profile = new JsonSampleProfiler().Profile(samples);

        Assert.Equal(2, profile.TotalSamples);
        Assert.Equal(2, profile.ValidSamples);
        Assert.Equal(0, profile.InvalidSamples);
        Assert.Equal(0, profile.TruncatedSamples);
        Assert.False(profile.WasLimitReached);
        AssertPath(profile, "$", 2, 2, "Object");
        AssertPath(profile, "$/name", 2, 2, "Null", "String");
        AssertPath(profile, "$/tags", 2, 2, "Array");
        AssertPath(profile, "$/tags[]", 1, 2, "String");
        AssertPath(profile, "$/address/city", 1, 1, "String");
        AssertPath(profile, "$/extra", 1, 1, "Number");
    }

    [Fact]
    public void SeparatesInvalidAndTruncatedSamples()
    {
        ColumnSample[] samples =
        [
            new(1, "[1,true]", false),
            new(2, "{", false),
            new(3, "{\"unfinished\":", true)
        ];

        JsonSampleProfile profile = new JsonSampleProfiler().Profile(samples);

        Assert.Equal(3, profile.TotalSamples);
        Assert.Equal(1, profile.ValidSamples);
        Assert.Equal(1, profile.InvalidSamples);
        Assert.Equal(1, profile.TruncatedSamples);
        AssertPath(profile, "$[]", 1, 2, "Boolean", "Number");
        Assert.Contains("1/2 complete", profile.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public void EscapesJsonPointerCharactersInPropertyNames()
    {
        JsonSampleProfile profile = new JsonSampleProfiler().Profile(
            [new ColumnSample(1, "{\"a/b~c\":true}", false)]);

        AssertPath(profile, "$/a~1b~0c", 1, 1, "Boolean");
    }

    [Fact]
    public void StopsAtTheDocumentDepthLimit()
    {
        string json = "{}";
        for (int depth = 0; depth <= JsonSampleProfiler.MaximumDepth; depth++)
        {
            json = $"{{\"level\":{json}}}";
        }

        JsonSampleProfile profile = new JsonSampleProfiler().Profile(
            [new ColumnSample(1, json, false)]);

        Assert.Equal(1, profile.ValidSamples);
        Assert.True(profile.WasLimitReached);
        Assert.Equal(JsonSampleProfiler.MaximumDepth + 1, profile.Paths.Count);
    }

    private static void AssertPath(
        JsonSampleProfile profile,
        string path,
        int documentCount,
        int valueCount,
        params string[] valueKinds)
    {
        JsonPathProfile actual = Assert.Single(profile.Paths, item => item.Path == path);
        Assert.Equal(documentCount, actual.DocumentCount);
        Assert.Equal(valueCount, actual.ValueCount);
        Assert.Equal(valueKinds, actual.ValueKinds);
    }
}
