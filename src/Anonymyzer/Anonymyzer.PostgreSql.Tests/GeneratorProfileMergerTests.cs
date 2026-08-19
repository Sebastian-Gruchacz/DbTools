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

    [Fact]
    public void KeepsManagedProfilesForDifferentLocales()
    {
        GeneratorProfileConfiguration polish = Create("PersonIdentity:Default", "Polish", "Language pack: Polish 1.0.0", "pl");
        polish.Locale = "pl-PL";
        GeneratorProfileConfiguration english = Create("PersonIdentity:en-US:Default", "English", "Language pack: English 1.0.0", "en");
        english.Locale = "en-US";
        var profiles = new List<GeneratorProfileConfiguration>();

        GeneratorProfileMergeResult result = new GeneratorProfileMerger().Merge(profiles, [polish, english]);

        Assert.Equal(2, result.AddedProfiles);
        Assert.Equal(2, profiles.Count);
        Assert.Contains(profiles, profile => profile.Locale == "pl-PL");
        Assert.Contains(profiles, profile => profile.Locale == "en-US");
        Assert.All(profiles, profile => Assert.StartsWith("Language pack:", profile.Origin));
    }

    [Fact]
    public void MigratesLegacyBuiltInLocaleStoredOnlyInOptions()
    {
        GeneratorProfileConfiguration legacy = Create("PersonIdentity:Default", "Legacy", "Built-in", "old");
        legacy.Options["Locale"] = "pl-PL";
        GeneratorProfileConfiguration current = Create(
            "PersonIdentity:Default",
            "Polish",
            "Language pack: Polish 1.0.0",
            "new");
        current.Locale = "pl-PL";
        current.Options["Locale"] = "pl-PL";
        var profiles = new List<GeneratorProfileConfiguration> { legacy };

        GeneratorProfileMergeResult result = new GeneratorProfileMerger().Merge(profiles, [current]);

        Assert.Equal(0, result.AddedProfiles);
        Assert.Equal(1, result.UpdatedProfiles);
        Assert.Single(profiles);
        Assert.Equal("pl-PL", legacy.Locale);
        Assert.Equal("Language pack: Polish 1.0.0", legacy.Origin);
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
