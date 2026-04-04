using VisualSqlArchitect.UI.Services.Canvas.AutoJoin;
using VisualSqlArchitect.UI.Services.Explain;
using VisualSqlArchitect.CanvasKit;
using VisualSqlArchitect.UI.ViewModels;
using Xunit;

namespace VisualSqlArchitect.Tests.Unit.ViewModels.Canvas;

public class CanvasAutoJoinClauseParsingTests
{
    [Theory]
    [InlineData("orders.customer_id = customers.id", true)]
    [InlineData("orders.total >= customers.min_total", false)]
    [InlineData("orders.total <= customers.max_total", false)]
    [InlineData("orders.id != customers.id", false)]
    [InlineData("orders.id == customers.id", false)]
    [InlineData("orders.id = customers.id AND 1=1", false)]
    public void TrySplitJoinClauseOnEquality_ValidatesOperatorShape(string clause, bool expected)
    {
        bool success = CanvasAutoJoinSemantics.TrySplitJoinClauseOnEquality(clause, out string left, out string right);

        Assert.Equal(expected, success);
        if (expected)
        {
            Assert.Equal("orders.customer_id", left);
            Assert.Equal("customers.id", right);
        }
    }
}


