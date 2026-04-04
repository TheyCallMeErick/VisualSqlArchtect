using VisualSqlArchitect.UI.Services.Canvas.AutoJoin;
using VisualSqlArchitect.UI.Services.Explain;
using VisualSqlArchitect.UI.ViewModels;
using VisualSqlArchitect.UI.ViewModels.Canvas;

namespace VisualSqlArchitect.Tests.Unit.ViewModels.Canvas;

public class ExplainPlanIndexSuggestionsTests
{
    [Fact]
    public void SelectIndexSuggestion_StoresSqlForUiConsumption()
    {
        var canvas = new CanvasViewModel();
        var sut = new ExplainPlanViewModel(canvas);
        var suggestion = new ExplainIndexSuggestion
        {
            Table = "orders",
            Columns = ["status"],
            Reason = "reason",
            Sql = "CREATE INDEX CONCURRENTLY idx_orders_status ON orders (status);",
        };

        sut.SelectIndexSuggestion(suggestion);

        Assert.Equal(suggestion.Sql, sut.SelectedSuggestionSql);
    }
}


