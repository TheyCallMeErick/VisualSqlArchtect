using AkkornStudio.Core;
using AkkornStudio.Nodes;
using AkkornStudio.UI.Services.QueryPreview;
using AkkornStudio.UI.ViewModels;
using static AkkornStudio.Tests.Unit.ViewModels.QueryPreview.QueryPreviewTestNodeFactory;

namespace AkkornStudio.Tests.Unit.ViewModels.QueryPreview;

public sealed class QueryGraphBuilderReportOutputTests
{
    [Fact]
    public void BuildSql_NoQuerySource_ReturnsGuidanceSql()
    {
        var canvas = new CanvasViewModel();
        canvas.Nodes.Clear();
        canvas.Connections.Clear();

        var sut = new QueryGraphBuilder(canvas, DatabaseProvider.Postgres);

        (string sql, List<string> errors) = sut.BuildSql();

        Assert.Empty(errors);
        Assert.Contains("Add a table", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildSql_WithResultOutputFlow_RemainsOperational()
    {
        var canvas = new CanvasViewModel();
        canvas.Nodes.Clear();
        canvas.Connections.Clear();

        NodeViewModel table = Table("public.orders", ("id", PinDataType.Number));
        NodeViewModel resultOutput = Node(NodeType.ResultOutput);
        Connect(canvas, table, "id", resultOutput, "column");

        canvas.Nodes.Add(table);
        canvas.Nodes.Add(resultOutput);

        var sut = new QueryGraphBuilder(canvas, DatabaseProvider.Postgres);

        (string sql, List<string> errors) = sut.BuildSql();

        Assert.Empty(errors);
        Assert.Contains("from", sql, StringComparison.OrdinalIgnoreCase);
    }
}
