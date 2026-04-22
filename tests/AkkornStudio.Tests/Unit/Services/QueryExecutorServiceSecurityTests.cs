using System.Reflection;
using AkkornStudio.Core;
using AkkornStudio.UI.Services;
using Xunit;

namespace AkkornStudio.Tests.Unit.Services;

public class QueryExecutorServiceSecurityTests
{
    [Theory]
    [InlineData("INSERT INTO users(id) VALUES (1)")]
    [InlineData("UPDATE users SET name = 'x'")]
    [InlineData("DELETE FROM users")]
    [InlineData("DROP TABLE users")]
    [InlineData("SELECT 1; DELETE FROM users")]
    public void ValidatePreviewQuery_RejectsMutatingOrMultiStatementSql(string sql)
    {
        MethodInfo validate = typeof(QueryExecutorService)
            .GetMethod("ValidatePreviewQuery", BindingFlags.NonPublic | BindingFlags.Static)!;

        var ex = Assert.Throws<TargetInvocationException>(() => validate.Invoke(null, [sql, null]));
        Assert.IsType<ArgumentException>(ex.InnerException);
    }

    [Theory]
    [InlineData("SELECT * FROM users WHERE id = @id")]
    [InlineData("SELECT * FROM users WHERE id = :id")]
    [InlineData("SELECT * FROM users WHERE id = ?")]
    [InlineData("SELECT * FROM users WHERE id = $1")]
    public void ValidatePreviewQuery_RejectsParameterPlaceholdersWithoutValues(string sql)
    {
        MethodInfo validate = typeof(QueryExecutorService)
            .GetMethod("ValidatePreviewQuery", BindingFlags.NonPublic | BindingFlags.Static)!;

        var ex = Assert.Throws<TargetInvocationException>(() => validate.Invoke(null, [sql, null]));
        Assert.IsType<ArgumentException>(ex.InnerException);
    }

    [Theory]
    [InlineData("SELECT * FROM users")]
    [InlineData("WITH q AS (SELECT 1) SELECT * FROM q")]
    [InlineData("EXPLAIN SELECT * FROM users")]
    [InlineData("SELECT @@VERSION")]
    public void ValidatePreviewQuery_AcceptsReadOnlyStatements(string sql)
    {
        MethodInfo validate = typeof(QueryExecutorService)
            .GetMethod("ValidatePreviewQuery", BindingFlags.NonPublic | BindingFlags.Static)!;

        Exception? ex = Record.Exception(() => validate.Invoke(null, [sql, null]));
        Assert.Null(ex);
    }

    [Theory]
    [InlineData("SELECT * FROM users WHERE id = @id", "@id", 1)]
    [InlineData("SELECT * FROM users WHERE id = :id", "id", 1)]
    public void ValidatePreviewQuery_AcceptsNamedParametersWhenValuesAreProvided(string sql, string parameterName, int value)
    {
        MethodInfo validate = typeof(QueryExecutorService)
            .GetMethod("ValidatePreviewQuery", BindingFlags.NonPublic | BindingFlags.Static)!;

        var parameters = new[] { new QueryParameter(parameterName, value) };

        Exception? ex = Record.Exception(() => validate.Invoke(null, [sql, parameters]));
        Assert.Null(ex);
    }

    [Theory]
    [InlineData("SELECT * FROM users WHERE id = ?", 1)]
    [InlineData("SELECT * FROM users WHERE id = $1", 1)]
    public void ValidatePreviewQuery_AcceptsPositionalParametersWhenValuesAreProvided(string sql, int value)
    {
        MethodInfo validate = typeof(QueryExecutorService)
            .GetMethod("ValidatePreviewQuery", BindingFlags.NonPublic | BindingFlags.Static)!;

        var parameters = new[] { new QueryParameter(null, value) };

        Exception? ex = Record.Exception(() => validate.Invoke(null, [sql, parameters]));
        Assert.Null(ex);
    }

    [Fact]
    public void WrapWithPreviewLimit_ClampsMaxRows()
    {
        MethodInfo wrap = typeof(QueryExecutorService)
            .GetMethod("WrapWithPreviewLimit", BindingFlags.NonPublic | BindingFlags.Static)!;

        string sqlLow = (string)wrap.Invoke(null, ["SELECT * FROM users", DatabaseProvider.Postgres, 0])!;
        string sqlHigh = (string)wrap.Invoke(null, ["SELECT * FROM users", DatabaseProvider.Postgres, 50000])!;

        Assert.Contains("LIMIT 1", sqlLow);
        Assert.Contains("LIMIT 10000", sqlHigh);
    }

    [Fact]
    public void QueryExecutorService_ExposesConfigurableCommandTimeout()
    {
        var executor = new QueryExecutorService();
        Assert.Equal(300, executor.CommandTimeoutSeconds);

        executor.CommandTimeoutSeconds = 42;
        Assert.Equal(42, executor.CommandTimeoutSeconds);
    }
}
