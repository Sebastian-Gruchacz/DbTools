namespace Anonymyzer.PostgreSql.Tests;

using Anonymyzer.Configuration;
using Newtonsoft.Json.Linq;

public sealed class GeneratorProfileMergerTests
{
    [Fact]
    public void PreservesFileProfileAndAddsBuiltInWithUniqueVisibleOrigin()
    {
        var fileProfile = Create("FixedText:Default", "File value", "Configuration file", "old");
        fileProfile.Origin = string.Empty;
        var profiles = new List<GeneratorProfileConfiguration> { fileProfile };
        GeneratorProfileConfiguration builtIn = Create("FixedText:Default", "Current default", "Built-in", "new");

        GeneratorProfileMergeResult result = new GeneratorProfileMerger().Merge(profiles, [builtIn]);

        Assert.True(result.Changed);
        Assert.Equal(1, result.AddedProfiles);
        Assert.Equal(1, result.IdCollisions);
        Assert.Equal(1, result.MarkedFileProfiles);
        Assert.Equal("Configuration file", profiles[0].Origin);
        Assert.Equal("old", profiles[0].Options.Value<string>("Value"));
        Assert.Equal("FixedText:Default:BuiltIn", profiles[1].Id);
        Assert.Equal("Built-in", profiles[1].Origin);
        Assert.Equal("new", profiles[1].Options.Value<string>("Value"));
    }

    [Fact]
    public void RefreshesExistingBuiltInWithoutCreatingAnotherProfile()
    {
        var profiles = new List<GeneratorProfileConfiguration>
        {
            Create("FixedText:Default:BuiltIn", "Old default", "Built-in", "old")
        };
        GeneratorProfileConfiguration builtIn = Create("FixedText:Default", "Current default", "Built-in", "new");

        GeneratorProfileMergeResult result = new GeneratorProfileMerger().Merge(profiles, [builtIn]);

        Assert.Equal(0, result.AddedProfiles);
        Assert.Equal(1, result.UpdatedProfiles);
        Assert.Single(profiles);
        Assert.Equal("Current default", profiles[0].DisplayName);
        Assert.Equal("new", profiles[0].Options.Value<string>("Value"));
    }

    private static GeneratorProfileConfiguration Create(
        string id,
        string displayName,
        string origin,
        string value) => new()
    {
        Id = id,
        DisplayName = displayName,
        GeneratorType = "FixedText",
        GeneratorVersion = "1.0.0",
        Origin = origin,
        Options = new JObject { ["Value"] = value }
    };
}
