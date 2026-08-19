namespace Anonymyzer.PostgreSql.Tests;

using Anonymyzer.Configuration;

public sealed class GeneratorProfileLocaleValidatorTests
{
    [Fact]
    public void ReportsOnlyProfilesWhoseLocaleIsInactive()
    {
        GeneratorProfileConfiguration[] profiles =
        [
            new() { Id = "Polish", Locale = "pl-PL" },
            new() { Id = "English", Locale = "en-US" },
            new() { Id = "Neutral" }
        ];

        IReadOnlyList<string> errors = GeneratorProfileLocaleValidator.Validate(profiles, ["en-US"]);

        string error = Assert.Single(errors);
        Assert.Contains("Profile 'Polish'", error);
        Assert.Contains("inactive locale 'pl-PL'", error);
    }
}
