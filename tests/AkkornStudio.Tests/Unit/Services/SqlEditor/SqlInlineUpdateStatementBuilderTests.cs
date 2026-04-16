using AkkornStudio.Core;
using AkkornStudio.UI.Services.SqlEditor;

namespace AkkornStudio.Tests.Unit.Services.SqlEditor;

public sealed class SqlInlineUpdateStatementBuilderTests
{
    [Fact]
    public void Build_Postgres_QuotesIdentifiersAndEscapesLiterals()
    {
        string sql = SqlInlineUpdateStatementBuilder.Build(
            DatabaseProvider.Postgres,
            "public.users",
            "name",
            "O'Hara",
            new Dictionary<string, object?> { ["id"] = 1 });

        Assert.Contains("UPDATE \"public\".\"users\" SET \"name\" = 'O''Hara'", sql, StringComparison.Ordinal);
        Assert.Contains("WHERE \"id\" = 1", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_SqlServer_UsesBracketQuoting()
    {
        string sql = SqlInlineUpdateStatementBuilder.Build(
            DatabaseProvider.SqlServer,
            "dbo.Users",
            "status",
            "active",
            new Dictionary<string, object?> { ["Id"] = 10 });

        Assert.Contains("UPDATE [dbo].[Users]", sql, StringComparison.Ordinal);
        Assert.Contains("[status] = 'active'", sql, StringComparison.Ordinal);
        Assert.Contains("[Id] = 10", sql, StringComparison.Ordinal);
    }
}
