namespace Anonymyzer.PostgreSql.Tests;

using Anonymyzer.Configuration;
using Newtonsoft.Json;

public class ConfigurationSerializationTests
{
    [Fact]
    public void SerializedConfigurationDoesNotContainConnectionStrings()
    {
        var configuration = new AnonymizationConfiguration
        {
            Database = new DatabaseTargetConfiguration
            {
                DatabaseEngine = "PostgreSql",
                DatabaseName = "detached_clone"
            }
        };

        string json = JsonConvert.SerializeObject(configuration);

        Assert.Contains("PostgreSql", json);
        Assert.Contains("detached_clone", json);
        Assert.DoesNotContain("ConnectionString", json);
    }

    [Fact]
    public void RoundTripPreservesProfilesCandidatesAndMultiColumnGroups()
    {
        var configuration = new AnonymizationConfiguration
        {
            GeneratorProfiles =
            {
                new GeneratorProfileConfiguration
                {
                    Id = "PolishPerson:Default",
                    DisplayName = "Polish person",
                    GeneratorType = "PersonIdentity",
                    GeneratorVersion = "1.0.0",
                    Locale = "pl-PL"
                }
            },
            Tables =
            {
                new TableProcessingOptions
                {
                    SchemaName = "public",
                    TableName = "customers",
                    Columns =
                    {
                        new ColumnProcessingOptions
                        {
                            Ordinal = 1,
                            ColumnName = "first_name",
                            SemanticRole = "Person.FirstName",
                            GenerationGroupId = "person",
                            Detection = new CandidateDetectionConfiguration
                            {
                                IsCandidate = true,
                                SuggestedRole = "Person.FirstName",
                                Locale = "en",
                                Confidence = 0.95m,
                                MatchedRule = "first_name"
                            }
                        }
                    },
                    GenerationGroups =
                    {
                        new GenerationGroupConfiguration
                        {
                            Id = "person",
                            GeneratorType = "PersonIdentity",
                            GeneratorVersion = "1.0.0",
                            ProfileId = "PolishPerson:Default",
                            Locale = "pl-PL",
                            Bindings = { ["FirstName"] = "first_name", ["LastName"] = "last_name" }
                        }
                    }
                }
            }
        };

        string json = JsonConvert.SerializeObject(configuration);
        AnonymizationConfiguration? restored = JsonConvert.DeserializeObject<AnonymizationConfiguration>(json);

        Assert.NotNull(restored);
        Assert.Equal("0.3.0", restored.Version);
        Assert.True(restored.Tables[0].HasCandidates);
        Assert.Equal("first_name", restored.Tables[0].GenerationGroups[0].Bindings["FirstName"]);
        Assert.DoesNotContain("HasCandidates", json);
    }

    [Fact]
    public void ValidatorRejectsBrokenProfileAndGroupReferences()
    {
        var configuration = new AnonymizationConfiguration
        {
            Tables =
            {
                new TableProcessingOptions
                {
                    SchemaName = "public",
                    TableName = "customers",
                    Columns =
                    {
                        new ColumnProcessingOptions
                        {
                            ColumnName = "email",
                            GenerationGroupId = "missing-group",
                            Generator = new ColumnGeneratorConfiguration { ProfileId = "missing-profile" }
                        }
                    }
                }
            }
        };

        IReadOnlyList<string> errors = ConfigurationValidator.Validate(configuration);

        Assert.Contains(errors, error => error.Contains("missing-profile", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains("missing-group", StringComparison.Ordinal));
    }
}
