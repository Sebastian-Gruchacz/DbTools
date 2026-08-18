namespace Anonymyzer.PostgreSql.Tests;

using Anonymyzer.Base.Generation;
using Anonymyzer.Generators.Simple;
using Newtonsoft.Json.Linq;

public class JsonPathRedactorGeneratorTests
{
    [Fact]
    public async Task ReplacesConfiguredValuesAndLeavesTheRestOfDocumentUntouched()
    {
        JsonPathRedactorGeneratorConfiguration configuration = Configure(
            ("$/person/name", "\"Anonymous\""),
            ("$/events[]/driverId", "0"));
        string result = await ApplyAsync(
            """{"person":{"name":"Alice","active":true},"events":[{"driverId":17,"kind":"login"},{"driverId":23,"kind":"logout"}]}""",
            configuration);

        Assert.True(JToken.DeepEquals(
            JToken.Parse("""{"person":{"name":"Anonymous","active":true},"events":[{"driverId":0,"kind":"login"},{"driverId":0,"kind":"logout"}]}"""),
            JToken.Parse(result)));
    }

    [Fact]
    public async Task SupportsRootArraysEscapedPropertiesAndTypedReplacementValues()
    {
        JsonPathRedactorGeneratorConfiguration configuration = Configure(
            ("$[]/tax~1id", "null"),
            ("$[]/flags~0raw", "[false,1]"),
            ("$[]/", "\"empty-key\""));
        string result = await ApplyAsync(
            """[{"tax/id":"123","flags~raw":true,"":"first"},{"tax/id":"456","flags~raw":false,"":"second"}]""",
            configuration);

        Assert.True(JToken.DeepEquals(
            JToken.Parse("""[{"tax/id":null,"flags~raw":[false,1],"":"empty-key"},{"tax/id":null,"flags~raw":[false,1],"":"empty-key"}]"""),
            JToken.Parse(result)));
    }

    [Fact]
    public async Task MissingPathsCanBeIgnoredOrRequired()
    {
        JsonPathRedactorGeneratorConfiguration optional = Configure(("$/missing", "0"));
        JsonPathRedactorGeneratorConfiguration required = Configure(("$/missing", "0"));
        required.RequireEveryPath = true;

        Assert.Equal("{\"present\":1}", await ApplyAsync("{\"present\":1}", optional));
        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => ApplyAsync("{\"present\":1}", required));
        Assert.Contains("$/missing", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PreservesDatabaseNullAndDoesNotEchoInvalidJsonInErrors()
    {
        var generator = new JsonPathRedactorGenerator();
        IGeneratorSession session = await PrepareAsync(generator, Configure(("$", "null")));
        await using (session)
        {
            var nullRow = new FakeGeneratorRow(null);
            await session.ApplyAsync(nullRow, TestContext.Current.CancellationToken);
            Assert.Null(nullRow.Value);

            const string secretInvalidJson = "{secret-value";
            var invalidRow = new FakeGeneratorRow(secretInvalidJson);
            InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => session.ApplyAsync(invalidRow, TestContext.Current.CancellationToken).AsTask());
            Assert.DoesNotContain(secretInvalidJson, exception.Message, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void CodecRejectsInvalidDuplicateAndOverlappingRules()
    {
        var codec = new JsonPathRedactorGeneratorConfigurationCodec();
        JsonPathRedactorGeneratorConfiguration configuration = Configure(
            ("$/person", "{}"),
            ("$/person/name", "\"x\""),
            ("$/person", "invalid"),
            ("$/bad~escape", "0"));

        IReadOnlyList<string> errors = codec.Validate(configuration);

        Assert.Contains(errors, error => error.Contains("overlap", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(errors, error => error.Contains("more than once", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(errors, error => error.Contains("not valid JSON", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(errors, error => error.Contains("invalid JSON Pointer escape", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void CodecRoundTripsRules()
    {
        var codec = new JsonPathRedactorGeneratorConfigurationCodec();
        JsonPathRedactorGeneratorConfiguration source = Configure(("$/email", "\"hidden@example.invalid\""));
        source.RequireEveryPath = true;

        JObject json = codec.Serialize(source);
        var restored = (JsonPathRedactorGeneratorConfiguration)codec.Deserialize(json);

        Assert.True(restored.RequireEveryPath);
        Assert.Single(restored.Rules);
        Assert.Equal("$/email", restored.Rules[0].Path);
        Assert.Equal("\"hidden@example.invalid\"", restored.Rules[0].ReplacementJson);
    }

    private static JsonPathRedactorGeneratorConfiguration Configure(params (string Path, string Replacement)[] rules) =>
        new()
        {
            Rules = rules.Select(rule => new JsonPathRedactionRuleConfiguration
            {
                Path = rule.Path,
                ReplacementJson = rule.Replacement
            }).ToList()
        };

    private static async Task<string> ApplyAsync(
        string source,
        JsonPathRedactorGeneratorConfiguration configuration)
    {
        var generator = new JsonPathRedactorGenerator();
        await using IGeneratorSession session = await PrepareAsync(generator, configuration);
        var row = new FakeGeneratorRow(source);
        await session.ApplyAsync(row, TestContext.Current.CancellationToken);
        return Assert.IsType<string>(row.Value);
    }

    private static async ValueTask<IGeneratorSession> PrepareAsync(
        JsonPathRedactorGenerator generator,
        JsonPathRedactorGeneratorConfiguration configuration)
    {
        GeneratorBinding binding = new(
            new GeneratorTableReference("dbo", "Documents"),
            new Dictionary<string, string> { [JsonPathRedactorGenerator.ValueOutput] = "Payload" });
        return await generator.PrepareAsync(
            new GeneratorPreparationContext(binding, new RejectingDataReader()),
            configuration,
            TestContext.Current.CancellationToken);
    }

    private sealed class FakeGeneratorRow(object? value) : IGeneratorRow
    {
        public object? Value { get; private set; } = value;

        public object? GetValue(string columnName) => Value;

        public void SetValue(string columnName, object? value) => Value = value;
    }

    private sealed class RejectingDataReader : IGeneratorDataReader
    {
        public IAsyncEnumerable<GeneratorDataRow> ReadAsync(
            GeneratorDataRequirement requirement,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("JSON row generator must not read source data.");
    }
}
