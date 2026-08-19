namespace Anonymyzer.PostgreSql.Tests;

using Anonymyzer.Base.Generation;
using Anonymyzer.Console.Planning;

public sealed class PostExecutionDatabaseValidatorTests
{
    [Fact]
    public void BuildsCompositeMatchSimpleForeignKeyCheckWithQuotedIdentifiers()
    {
        var constraint = new PostgreSqlForeignKey(
            "FK order tenant",
            new GeneratorTableReference("sales", "order lines"),
            new GeneratorTableReference("catalog", "products"),
            MatchFull: false,
            ["tenant_id", "product_id"],
            ["tenant_id", "id"]);

        string sql = PostExecutionDatabaseValidator.BuildPostgreSqlForeignKeyViolationQuery(constraint);

        Assert.Contains("FROM \"sales\".\"order lines\" AS child", sql, StringComparison.Ordinal);
        Assert.Contains("child.\"tenant_id\" = parent.\"tenant_id\"", sql, StringComparison.Ordinal);
        Assert.Contains("child.\"product_id\" = parent.\"id\"", sql, StringComparison.Ordinal);
        Assert.Contains("child.\"tenant_id\" IS NOT NULL", sql, StringComparison.Ordinal);
        Assert.Contains("child.\"product_id\" IS NOT NULL", sql, StringComparison.Ordinal);
        Assert.Contains("parent.\"tenant_id\" IS NULL", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void MatchFullCheckAlsoDetectsPartiallyNullCompositeKeys()
    {
        var constraint = new PostgreSqlForeignKey(
            "FK full",
            new GeneratorTableReference("public", "child"),
            new GeneratorTableReference("public", "parent"),
            MatchFull: true,
            ["left_id", "right_id"],
            ["left_id", "right_id"]);

        string sql = PostExecutionDatabaseValidator.BuildPostgreSqlForeignKeyViolationQuery(constraint);

        Assert.Contains("child.\"left_id\" IS NULL OR child.\"right_id\" IS NULL", sql, StringComparison.Ordinal);
        Assert.Contains("child.\"left_id\" IS NOT NULL OR child.\"right_id\" IS NOT NULL", sql, StringComparison.Ordinal);
    }
}
