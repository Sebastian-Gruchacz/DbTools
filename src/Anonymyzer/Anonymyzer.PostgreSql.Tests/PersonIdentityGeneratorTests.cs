namespace Anonymyzer.PostgreSql.Tests;

using Anonymyzer.Base.Generation;
using Anonymyzer.Generators.Person;
using Anonymyzer.LanguagePack.English;
using Anonymyzer.LanguagePack.Polish;
using Newtonsoft.Json.Linq;

public class PersonIdentityGeneratorTests
{
    [Fact]
    public void DescriptorDeclaresBindableSemanticOutputs()
    {
        var generator = new PersonIdentityGenerator(new[] { new PolishPersonLocaleDataProvider() });

        Assert.Equal(GeneratorExecutionScope.Row, generator.Descriptor.Scope);
        Assert.Collection(
            generator.Descriptor.Outputs,
            output => Assert.Equal((PersonIdentityGenerator.FirstNameOutput, "Person.FirstName"), (output.Name, output.SemanticRole)),
            output => Assert.Equal((PersonIdentityGenerator.LastNameOutput, "Person.LastName"), (output.Name, output.SemanticRole)),
            output => Assert.Equal((PersonIdentityGenerator.GenderOutput, "Person.Gender"), (output.Name, output.SemanticRole)),
            output => Assert.Equal((PersonIdentityGenerator.EmailOutput, "Contact.Email"), (output.Name, output.SemanticRole)));
    }

    [Fact]
    public async Task GeneratesCoherentPolishIdentityAndNameBasedEmailInOneRow()
    {
        var localeProvider = new PolishPersonLocaleDataProvider();
        var generator = new PersonIdentityGenerator(new[] { localeProvider });
        var configuration = new PersonIdentityGeneratorConfiguration
        {
            Seed = 123,
            Locale = "pl-PL",
            EmailPattern = PersonEmailPattern.NameBased,
            EmailDomain = "example.invalid"
        };
        GeneratorBinding binding = CreateBinding();
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await using IGeneratorSession session = await generator.PrepareAsync(
            new GeneratorPreparationContext(binding, new RejectingDataReader()),
            configuration,
            cancellationToken);
        var row = new DictionaryGeneratorRow();
        await session.ApplyAsync(row, cancellationToken);

        string firstName = Assert.IsType<string>(row.GetValue("first_name"));
        string lastName = Assert.IsType<string>(row.GetValue("last_name"));
        string email = Assert.IsType<string>(row.GetValue("email"));
        Assert.Equal($"{localeProvider.NormalizeEmailToken(firstName)}.{localeProvider.NormalizeEmailToken(lastName)}.000001@example.invalid", email);
        Assert.Contains(Assert.IsType<string>(row.GetValue("gender")), new[] { "Female", "Male" });
        Assert.Empty(generator.GetDataRequirements(binding, configuration));
    }

    [Fact]
    public async Task GeneratesCoherentEnglishIdentityAndNameBasedEmailInOneRow()
    {
        var localeProvider = new EnglishPersonLocaleDataProvider();
        var generator = new PersonIdentityGenerator(new IPersonLocaleDataProvider[]
        {
            new PolishPersonLocaleDataProvider(),
            localeProvider
        });
        var configuration = new PersonIdentityGeneratorConfiguration
        {
            Seed = 456,
            Locale = "en-US",
            EmailPattern = PersonEmailPattern.NameBased,
            EmailDomain = "example.invalid"
        };
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var row = new DictionaryGeneratorRow();

        await using IGeneratorSession session = await generator.PrepareAsync(
            new GeneratorPreparationContext(CreateBinding(), new RejectingDataReader()),
            configuration,
            cancellationToken);
        await session.ApplyAsync(row, cancellationToken);

        string firstName = Assert.IsType<string>(row.GetValue("first_name"));
        string lastName = Assert.IsType<string>(row.GetValue("last_name"));
        Assert.Equal(
            $"{localeProvider.NormalizeEmailToken(firstName)}.{localeProvider.NormalizeEmailToken(lastName)}.000001@example.invalid",
            row.GetValue("email"));
        Assert.Contains(Assert.IsType<string>(row.GetValue("gender")), new[] { "Female", "Male" });
    }

    [Theory]
    [InlineData("José O'Connor", "joseoconnor")]
    [InlineData("Anne-Marie 42", "annemarie42")]
    public void EnglishPackNormalizesEmailTokens(string value, string expected)
    {
        Assert.Equal(expected, new EnglishPersonLocaleDataProvider().NormalizeEmailToken(value));
    }

    [Fact]
    public async Task SameSeedProducesSameSequenceAndUniqueEmails()
    {
        var localeProvider = new PolishPersonLocaleDataProvider();
        var configuration = new PersonIdentityGeneratorConfiguration { Seed = 77 };
        GeneratorBinding binding = CreateBinding();
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        string[] firstRun = await GenerateEmails(new PersonIdentityGenerator(new[] { localeProvider }), binding, configuration, 20, cancellationToken);
        string[] secondRun = await GenerateEmails(new PersonIdentityGenerator(new[] { localeProvider }), binding, configuration, 20, cancellationToken);

        Assert.Equal(firstRun, secondRun);
        Assert.Equal(firstRun.Length, firstRun.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void CodecSerializesOwnedConfigurationAsJson()
    {
        var codec = new PersonIdentityGeneratorConfigurationCodec();
        var configuration = new PersonIdentityGeneratorConfiguration
        {
            Seed = 19,
            Locale = "pl-PL",
            EmailPattern = PersonEmailPattern.Opaque,
            EmailDomain = "example.invalid"
        };

        JObject json = codec.Serialize(configuration);
        var restored = (PersonIdentityGeneratorConfiguration)codec.Deserialize(json);

        Assert.Equal("Opaque", json[nameof(configuration.EmailPattern)]?.Value<string>());
        Assert.Equal(PersonEmailPattern.Opaque, restored.EmailPattern);
        Assert.Empty(codec.Validate(restored));
    }

    [Fact]
    public void PolishPackNormalizesDiacriticsAndKeepsGenderedSurnameFormsCoherent()
    {
        var localeProvider = new PolishPersonLocaleDataProvider();
        var random = new Random(9123);

        Assert.Equal("lukaszzolc", localeProvider.NormalizeEmailToken("Łukasz Żółć"));
        for (int index = 0; index < 500; index++)
        {
            GeneratedPersonName person = localeProvider.GenerateName(random);
            if (person.LastName.EndsWith("ska", StringComparison.Ordinal))
            {
                Assert.Equal(PersonGender.Female, person.Gender);
            }
            else if (person.LastName.EndsWith("ski", StringComparison.Ordinal))
            {
                Assert.Equal(PersonGender.Male, person.Gender);
            }
        }
    }

    private static GeneratorBinding CreateBinding()
    {
        return new GeneratorBinding(
            new GeneratorTableReference("public", "people"),
            new Dictionary<string, string>
            {
                [PersonIdentityGenerator.FirstNameOutput] = "first_name",
                [PersonIdentityGenerator.LastNameOutput] = "last_name",
                [PersonIdentityGenerator.GenderOutput] = "gender",
                [PersonIdentityGenerator.EmailOutput] = "email"
            });
    }

    private static async Task<string[]> GenerateEmails(
        PersonIdentityGenerator generator,
        GeneratorBinding binding,
        PersonIdentityGeneratorConfiguration configuration,
        int count,
        CancellationToken cancellationToken)
    {
        await using IGeneratorSession session = await generator.PrepareAsync(
            new GeneratorPreparationContext(binding, new RejectingDataReader()),
            configuration,
            cancellationToken);
        var emails = new List<string>();
        for (int index = 0; index < count; index++)
        {
            var row = new DictionaryGeneratorRow();
            await session.ApplyAsync(row, cancellationToken);
            emails.Add(Assert.IsType<string>(row.GetValue("email")));
        }

        return emails.ToArray();
    }

    private sealed class RejectingDataReader : IGeneratorDataReader
    {
        public IAsyncEnumerable<GeneratorDataRow> ReadAsync(
            GeneratorDataRequirement requirement,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("A row-local generator must not request a database scan.");
        }
    }

    private sealed class DictionaryGeneratorRow : IGeneratorRow
    {
        private readonly Dictionary<string, object?> _values = new();

        public object? GetValue(string columnName) => _values[columnName];

        public void SetValue(string columnName, object? value) => _values[columnName] = value;
    }
}
