namespace Anonymyzer.PostgreSql.Tests;

using Anonymyzer.Base;
using Anonymyzer.Console;
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
}
