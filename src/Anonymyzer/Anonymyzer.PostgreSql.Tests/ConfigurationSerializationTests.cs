namespace Anonymyzer.PostgreSql.Tests;

using Anonymyzer.Console.Configuration;
using Newtonsoft.Json;

public class ConfigurationSerializationTests
{
    [Fact]
    public void SerializedConfigurationDoesNotContainConnectionStrings()
    {
        var configuration = new AnonymyzationConfiguration
        {
            DbConfiguration = new DatabaseTargetConfiguration
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
}
