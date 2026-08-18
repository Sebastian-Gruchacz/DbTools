namespace Anonymyzer.PostgreSql.Tests;

using Anonymyzer.Base;
using Anonymyzer.Base.Generation;
using Anonymyzer.Console;
using Anonymyzer.Generators.Person;
using Anonymyzer.Generators.Simple;
using Anonymyzer.PostgreSql;
using Anonymyzer.SqlServer;
using Microsoft.Extensions.DependencyInjection;

public class DatabaseEngineRegistrationTests
{
    [Fact]
    public void RegistersBothSupportedDatabaseEngines()
    {
        using ServiceProvider provider = new ServiceCollection()
            .AddDatabaseEngines()
            .BuildServiceProvider();

        IAnonymyzerEngineBuilder[] builders = provider
            .GetServices<IAnonymyzerEngineBuilder>()
            .ToArray();

        Assert.Contains(builders, builder => builder is SqlServerEngineBuilder);
        Assert.Contains(builders, builder => builder is PostgreSqlEngineBuilder);
    }

    [Fact]
    public void RegistersBuiltInGeneratorsAndPolishLanguagePack()
    {
        using ServiceProvider provider = new ServiceCollection()
            .AddBuiltInGenerators()
            .BuildServiceProvider();

        IGenerator[] generators = provider.GetServices<IGenerator>().ToArray();

        Assert.Contains(generators, generator => generator is PersonIdentityGenerator);
        Assert.Contains(generators, generator => generator.Descriptor.Type == "TextShuffler");
        Assert.Contains(generators, generator => generator is FixedTextGenerator);
        Assert.Contains(generators, generator => generator is SequentialTextGenerator);
        Assert.Contains(generators, generator => generator is EmailAddressGenerator);
        Assert.Contains(generators, generator => generator is PhoneNumberGenerator);
        Assert.Contains(generators, generator => generator is UuidGenerator);
    }
}
