namespace Anonymyzer.PostgreSql.Tests;

using Npgsql;

public sealed class PostgreSqlConnectionBuilderTests
{
    [Fact]
    public void BuildMainConnectionAddsDatabaseWhenMissing()
    {
        var builder = new PostgreSqlConnectionBuilder();

        using var connection = (NpgsqlConnection)builder.BuildMainConnection(
            "Host=localhost;Username=test;Password=test",
            "sample_database");

        Assert.Equal("sample_database", connection.Database);
    }

    [Fact]
    public void BuildMainConnectionPreservesDatabaseFromConnectionString()
    {
        var builder = new PostgreSqlConnectionBuilder();

        using var connection = (NpgsqlConnection)builder.BuildMainConnection(
            "Host=localhost;Username=test;Password=test;Database=configured_database",
            "ignored_database");

        Assert.Equal("configured_database", connection.Database);
    }
}
