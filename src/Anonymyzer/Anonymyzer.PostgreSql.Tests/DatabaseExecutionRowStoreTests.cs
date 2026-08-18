namespace Anonymyzer.PostgreSql.Tests;

using Anonymyzer.Base;
using Anonymyzer.Base.Generation;
using Anonymyzer.Console.Planning;

public sealed class DatabaseExecutionRowStoreTests
{
    [Fact]
    public void BuildsKeysetQueriesForBothDatabaseEngines()
    {
        var table = new GeneratorTableReference("app", "people");

        string sqlServer = DatabaseExecutionRowStore.BuildSelect(
            "SqlServer",
            table,
            "id",
            ["first_name", "last_name"],
            hasAfterKey: true);
        string postgreSql = DatabaseExecutionRowStore.BuildSelect(
            "PostgreSql",
            table,
            "id",
            ["first_name", "last_name"],
            hasAfterKey: true);

        Assert.Equal(
            "SELECT TOP (@take) [id], [first_name], [last_name] FROM [app].[people] " +
            "WHERE [id] > @after_key ORDER BY [id];",
            sqlServer);
        Assert.Equal(
            "SELECT \"id\", \"first_name\", \"last_name\" FROM \"app\".\"people\" " +
            "WHERE \"id\" > @after_key ORDER BY \"id\" LIMIT @take;",
            postgreSql);
    }

    [Fact]
    public void BuildsParameterizedUpdateAndEscapesIdentifiers()
    {
        var table = new GeneratorTableReference("odd]schema", "user\"data");

        string sqlServer = DatabaseExecutionRowStore.BuildUpdate(
            "SqlServer",
            table,
            "id]key",
            [new ExecutionOutputColumn("display]name", DbDataType.Text)]);
        string postgreSql = DatabaseExecutionRowStore.BuildUpdate(
            "PostgreSql",
            table,
            "id\"key",
            [new ExecutionOutputColumn("display\"name", DbDataType.Text)]);

        Assert.Equal(
            "UPDATE [odd]]schema].[user\"data] SET [display]]name] = @value_0 " +
            "WHERE [id]]key] = @primary_key;",
            sqlServer);
        Assert.Equal(
            "UPDATE \"odd]schema\".\"user\"\"data\" SET \"display\"\"name\" = @value_0 " +
            "WHERE \"id\"\"key\" = @primary_key;",
            postgreSql);
    }

    [Fact]
    public void CastsOnlyPostgreSqlJsonOutputs()
    {
        var table = new GeneratorTableReference("app", "documents");
        ExecutionOutputColumn[] columns =
        [
            new("payload", DbDataType.Json),
            new("description", DbDataType.Text)
        ];

        string postgreSql = DatabaseExecutionRowStore.BuildUpdate("PostgreSql", table, "id", columns);
        string sqlServer = DatabaseExecutionRowStore.BuildUpdate("SqlServer", table, "id", columns);

        Assert.Contains("\"payload\" = CAST(@value_0 AS jsonb)", postgreSql, StringComparison.Ordinal);
        Assert.Contains("\"description\" = @value_1", postgreSql, StringComparison.Ordinal);
        Assert.Contains("[payload] = @value_0", sqlServer, StringComparison.Ordinal);
    }
}
