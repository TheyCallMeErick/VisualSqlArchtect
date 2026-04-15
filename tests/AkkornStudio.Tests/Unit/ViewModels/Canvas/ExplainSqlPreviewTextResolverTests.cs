using AkkornStudio.UI.Services.Canvas.AutoJoin;
using AkkornStudio.UI.Services.Explain;
using AkkornStudio.UI.ViewModels.Canvas;

namespace AkkornStudio.Tests.Unit.ViewModels.Canvas;

public class ExplainSqlPreviewTextResolverTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Resolve_ReturnsFallback_WhenSqlMissing(string? sql)
    {
        var sut = new ExplainSqlPreviewTextResolver();
        Assert.Equal("No SQL available.", sut.Resolve(sql));
    }

    [Fact]
    public void Resolve_ReturnsOriginalSql_WhenPresent()
    {
        const string sql = "SELECT * FROM orders WHERE id = 1";
        var sut = new ExplainSqlPreviewTextResolver();

        Assert.Equal(sql, sut.Resolve(sql));
    }
}


