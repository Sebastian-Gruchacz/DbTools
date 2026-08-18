namespace Anonymyzer.PostgreSql.Tests;

using Anonymyzer.Base;
using Anonymyzer.Base.Generation;
using Anonymyzer.Configuration;
using Anonymyzer.Console.Planning;
using Anonymyzer.Generators.Person;
using Anonymyzer.Generators.Simple;
using Anonymyzer.LanguagePack.Polish;
using Newtonsoft.Json.Linq;

public sealed class AnonymizationExecutionPlannerTests
{
    [Fact]
    public void BuildsRowAndColumnStepsWithDeclaredDataRequirements()
    {
        var person = new PersonIdentityGenerator(new[] { new PolishPersonLocaleDataProvider() });
        var shuffler = new ShufflingTextGenerator();
        AnonymizationConfiguration configuration = CreateBuiltInConfiguration(person, shuffler);
        configuration.Tables[0].Columns[2].Generator.Options[nameof(ShufflingTextGeneratorConfiguration.Seed)] = 987;
        var planner = new AnonymizationExecutionPlanner(new IGenerator[] { person, shuffler });

        AnonymizationExecutionPlan plan = planner.Build(configuration);

        Assert.Equal(AnonymizationExecutionPlanner.DefaultBatchSize, plan.BatchSize);
        Assert.Collection(
            plan.Steps,
            step =>
            {
                Assert.Equal("public.people/group:identity", step.Id);
                Assert.Equal(GeneratorExecutionScope.Row, step.Generator.Scope);
                Assert.Empty(step.DataRequirements);
            },
            step =>
            {
                Assert.Equal("public.people/column:notes", step.Id);
                Assert.Equal(GeneratorExecutionScope.Column, step.Generator.Scope);
                GeneratorDataRequirement requirement = Assert.Single(step.DataRequirements);
                Assert.True(requirement.RequiresCompleteScan);
                Assert.Equal(GeneratorValueSource.Original, requirement.ValueSource);
                Assert.Equal(new[] { "notes" }, requirement.Columns);
                Assert.Equal(987, Assert.IsType<ShufflingTextGeneratorConfiguration>(step.Configuration).Seed);
            });

        IReadOnlyList<string> lines = ExecutionPlanFormatter.Format(plan);
        Assert.Contains(lines, line => line.Contains("proposed batch size 1000", StringComparison.Ordinal));
        Assert.Contains(lines, line => line.Contains("complete scan", StringComparison.Ordinal));
    }

    [Fact]
    public void OrdersGeneratedValueProducerBeforeConsumerAcrossTables()
    {
        var producer = new TestGenerator("Producer", "public", "source", null);
        var consumer = new TestGenerator("Consumer", "public", "target", ("public", "source", "generated_value"));
        AnonymizationConfiguration configuration = CreateDependencyConfiguration(producer, consumer);
        var planner = new AnonymizationExecutionPlanner(new IGenerator[] { producer, consumer });

        AnonymizationExecutionPlan plan = planner.Build(configuration);

        Assert.Equal("public.source/column:generated_value", plan.Steps[0].Id);
        Assert.Equal("public.target/column:masked_value", plan.Steps[1].Id);
    }

    [Fact]
    public void RejectsGeneratedValueDependencyCycle()
    {
        var first = new TestGenerator("First", "public", "first", ("public", "second", "value"));
        var second = new TestGenerator("Second", "public", "second", ("public", "first", "value"));
        AnonymizationConfiguration configuration = CreateTwoTableConfiguration(first, second);
        var planner = new AnonymizationExecutionPlanner(new IGenerator[] { first, second });

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => planner.Build(configuration));

        Assert.Contains("dependency cycle", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RejectsEnabledColumnWithoutActiveBinding()
    {
        var person = new PersonIdentityGenerator(new[] { new PolishPersonLocaleDataProvider() });
        var configuration = new AnonymizationConfiguration
        {
            GeneratorProfiles = { CreateProfile("person", person) },
            Tables =
            {
                new TableProcessingOptions
                {
                    SchemaName = "public",
                    TableName = "people",
                    Enabled = true,
                    Columns =
                    {
                        new ColumnProcessingOptions
                        {
                            ColumnName = "first_name",
                            Enabled = true,
                            GenerationGroupId = "identity"
                        }
                    },
                    GenerationGroups =
                    {
                        new GenerationGroupConfiguration
                        {
                            Id = "identity",
                            GeneratorType = person.Descriptor.Type,
                            GeneratorVersion = person.Descriptor.Version,
                            ProfileId = "person",
                            Bindings = { [PersonIdentityGenerator.LastNameOutput] = "last_name" }
                        }
                    }
                }
            }
        };
        var planner = new AnonymizationExecutionPlanner(new[] { person });

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => planner.Build(configuration));

        Assert.Contains("has no active generator step", exception.Message);
    }

    [Fact]
    public void AcceptsAnyDataTypeDeclaredByGeneratorAndRejectsOthers()
    {
        var birthDate = new BirthDateGenerator();
        AnonymizationConfiguration configuration = new()
        {
            GeneratorProfiles = { CreateProfile("birth-date", birthDate) },
            Tables = { CreateTable("people", "birth_date", "birth-date", birthDate) }
        };
        configuration.Tables[0].Columns[0].DataType = DbDataType.DateTime.ToString();
        var planner = new AnonymizationExecutionPlanner([birthDate]);

        Assert.Single(planner.Build(configuration).Steps);

        configuration.Tables[0].Columns[0].DataType = DbDataType.Text.ToString();
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => planner.Build(configuration));
        Assert.Contains("Date or DateTime", exception.Message);
    }

    [Fact]
    public void OrdersBirthDateAndGenderBeforeDependentNationalIdentifier()
    {
        var birthDate = new BirthDateGenerator();
        var gender = new GenderGenerator();
        var nationalIdentifier = new NationalIdentifierGenerator([new PolishNationalIdentifierLocaleDataProvider()]);
        GeneratorProfileConfiguration nationalProfile = CreateProfile("national-id", nationalIdentifier);
        nationalProfile.Options = nationalIdentifier.Configuration.Serialize(new NationalIdentifierGeneratorConfiguration
        {
            BirthDateColumn = "birth_date",
            BirthDateValueSource = GeneratorValueSource.Generated,
            GenderColumn = "gender",
            GenderValueSource = GeneratorValueSource.Generated
        });
        AnonymizationConfiguration configuration = new()
        {
            GeneratorProfiles =
            {
                CreateProfile("birth-date", birthDate),
                CreateProfile("gender", gender),
                nationalProfile
            },
            Tables =
            {
                new TableProcessingOptions
                {
                    SchemaName = "public",
                    TableName = "people",
                    Enabled = true,
                    Columns =
                    {
                        CreateColumn(1, "birth_date", "birth-date", birthDate),
                        CreateColumn(2, "gender", "gender", gender),
                        CreateColumn(3, "national_id", "national-id", nationalIdentifier)
                    }
                }
            }
        };
        configuration.Tables[0].Columns[0].DataType = DbDataType.Date.ToString();
        configuration.Tables[0].Columns[1].DataType = DbDataType.Text.ToString();
        configuration.Tables[0].Columns[2].DataType = DbDataType.Text.ToString();
        var planner = new AnonymizationExecutionPlanner([nationalIdentifier, gender, birthDate]);

        AnonymizationExecutionPlan plan = planner.Build(configuration);

        Assert.Equal("public.people/column:national_id", plan.Steps[^1].Id);
        Assert.Equal(
            ["public.people/column:birth_date", "public.people/column:gender"],
            plan.Steps.Take(2).Select(step => step.Id).OrderBy(id => id));
    }

    private static AnonymizationConfiguration CreateBuiltInConfiguration(
        PersonIdentityGenerator person,
        ShufflingTextGenerator shuffler)
    {
        return new AnonymizationConfiguration
        {
            GeneratorProfiles =
            {
                CreateProfile("person", person),
                CreateProfile("shuffle", shuffler)
            },
            Tables =
            {
                new TableProcessingOptions
                {
                    SchemaName = "public",
                    TableName = "people",
                    Enabled = true,
                    Columns =
                    {
                        new ColumnProcessingOptions
                        {
                            Ordinal = 1,
                            ColumnName = "first_name",
                            Enabled = true,
                            GenerationGroupId = "identity"
                        },
                        new ColumnProcessingOptions
                        {
                            Ordinal = 2,
                            ColumnName = "email",
                            Enabled = true,
                            GenerationGroupId = "identity"
                        },
                        CreateColumn(3, "notes", "shuffle", shuffler)
                    },
                    GenerationGroups =
                    {
                        new GenerationGroupConfiguration
                        {
                            Id = "identity",
                            GeneratorType = person.Descriptor.Type,
                            GeneratorVersion = person.Descriptor.Version,
                            ProfileId = "person",
                            Bindings =
                            {
                                [PersonIdentityGenerator.FirstNameOutput] = "first_name",
                                [PersonIdentityGenerator.EmailOutput] = "email"
                            }
                        }
                    }
                }
            }
        };
    }

    private static AnonymizationConfiguration CreateDependencyConfiguration(
        TestGenerator producer,
        TestGenerator consumer)
    {
        return new AnonymizationConfiguration
        {
            GeneratorProfiles =
            {
                CreateProfile("consumer", consumer),
                CreateProfile("producer", producer)
            },
            Tables =
            {
                CreateTable("target", "masked_value", "consumer", consumer),
                CreateTable("source", "generated_value", "producer", producer)
            }
        };
    }

    private static AnonymizationConfiguration CreateTwoTableConfiguration(
        TestGenerator first,
        TestGenerator second)
    {
        return new AnonymizationConfiguration
        {
            GeneratorProfiles =
            {
                CreateProfile("first", first),
                CreateProfile("second", second)
            },
            Tables =
            {
                CreateTable("first", "value", "first", first),
                CreateTable("second", "value", "second", second)
            }
        };
    }

    private static TableProcessingOptions CreateTable(
        string tableName,
        string columnName,
        string profileId,
        IGenerator generator)
    {
        return new TableProcessingOptions
        {
            SchemaName = "public",
            TableName = tableName,
            Enabled = true,
            Columns = { CreateColumn(1, columnName, profileId, generator) }
        };
    }

    private static ColumnProcessingOptions CreateColumn(
        int ordinal,
        string columnName,
        string profileId,
        IGenerator generator)
    {
        return new ColumnProcessingOptions
        {
            Ordinal = ordinal,
            ColumnName = columnName,
            Enabled = true,
            Generator = new ColumnGeneratorConfiguration
            {
                GeneratorType = generator.Descriptor.Type,
                GeneratorVersion = generator.Descriptor.Version,
                ProfileId = profileId
            }
        };
    }

    private static GeneratorProfileConfiguration CreateProfile(string id, IGenerator generator)
    {
        return new GeneratorProfileConfiguration
        {
            Id = id,
            GeneratorType = generator.Descriptor.Type,
            GeneratorVersion = generator.Descriptor.Version,
            Options = generator.Configuration.Serialize(generator.Configuration.CreateDefault())
        };
    }

    private sealed class TestGenerator : IGenerator
    {
        private static readonly TestConfigurationCodec Codec = new();
        private readonly (string Schema, string Table, string Column)? _generatedRequirement;

        public TestGenerator(
            string type,
            string schemaName,
            string tableName,
            (string Schema, string Table, string Column)? generatedRequirement)
        {
            _generatedRequirement = generatedRequirement;
            Descriptor = new GeneratorDescriptor(
                type,
                "1.0.0",
                type,
                GeneratorExecutionScope.Relational,
                DbDataType.Text)
            {
                Outputs = new[] { new GeneratorOutputDescriptor("Value", "Value", string.Empty, Required: true) }
            };
        }

        public GeneratorDescriptor Descriptor { get; }

        public IGeneratorConfigurationCodec Configuration => Codec;

        public IReadOnlyList<GeneratorDataRequirement> GetDataRequirements(
            GeneratorBinding binding,
            object configuration)
        {
            return _generatedRequirement is { } requirement
                ? new[]
                {
                    new GeneratorDataRequirement(
                        "generated-input",
                        new GeneratorTableReference(requirement.Schema, requirement.Table),
                        new[] { requirement.Column },
                        GeneratorValueSource.Generated,
                        RequiresCompleteScan: false)
                }
                : Array.Empty<GeneratorDataRequirement>();
        }

        public ValueTask<IGeneratorSession> PrepareAsync(
            GeneratorPreparationContext context,
            object configuration,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class TestConfigurationCodec : IGeneratorConfigurationCodec
    {
        public Type ConfigurationType => typeof(object);

        public object CreateDefault() => new();

        public object Deserialize(JObject json) => new();

        public JObject Serialize(object configuration) => new();

        public IReadOnlyList<string> Validate(object configuration) => Array.Empty<string>();
    }
}
