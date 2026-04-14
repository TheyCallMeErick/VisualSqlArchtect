using DBWeaver.Core;
using DBWeaver.Nodes;
using DBWeaver.UI.Services.QueryPreview;
using DBWeaver.UI.ViewModels;
using static DBWeaver.Tests.Unit.ViewModels.QueryPreview.QueryPreviewTestNodeFactory;

namespace DBWeaver.Tests.Unit.ViewModels.QueryPreview;

public sealed class QueryGraphBuilderReportOutputTests
{
    [Fact]
    public void BuildSql_ReportOnlyLegacyFlow_ReturnsDiagnosticsInsteadOfRawSql()
    {
        NodeType rawSqlType = Enum.Parse<NodeType>("RawSqlQuery");
        NodeType reportOutputType = Enum.Parse<NodeType>("ReportOutput");

        var canvas = new CanvasViewModel();
        canvas.Nodes.Clear();
        canvas.Connections.Clear();

        NodeViewModel rawSql = Node(rawSqlType);
        rawSql.Parameters["sql"] = "SELECT 42 AS answer";
        NodeViewModel reportOutput = Node(reportOutputType);

        canvas.Nodes.Add(rawSql);
        canvas.Nodes.Add(reportOutput);
        Connect(canvas, rawSql, "query", reportOutput, "query");

        var sut = new QueryGraphBuilder(canvas, DatabaseProvider.Postgres);

        (string sql, List<string> errors) = sut.BuildSql();

        Assert.Empty(errors);
        Assert.Contains("Add a table", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SELECT 42 AS answer", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildSql_PrioritizesResultOutputWhenQueryAndReportFlowsCoexist()
    {
        NodeType rawSqlType = Enum.Parse<NodeType>("RawSqlQuery");
        NodeType reportOutputType = Enum.Parse<NodeType>("ReportOutput");

        var canvas = new CanvasViewModel();
        canvas.Nodes.Clear();
        canvas.Connections.Clear();

        NodeViewModel table = Table("public.orders", ("id", PinDataType.Number));
        NodeViewModel resultOutput = Node(NodeType.ResultOutput);
        Connect(canvas, table, "id", resultOutput, "column");

        NodeViewModel rawSql = Node(rawSqlType);
        rawSql.Parameters["sql"] = "SELECT should_not_be_used";
        NodeViewModel reportOutput = Node(reportOutputType);
        Connect(canvas, rawSql, "query", reportOutput, "query");

        canvas.Nodes.Add(table);
        canvas.Nodes.Add(resultOutput);
        canvas.Nodes.Add(rawSql);
        canvas.Nodes.Add(reportOutput);

        var sut = new QueryGraphBuilder(canvas, DatabaseProvider.Postgres);

        (string sql, List<string> errors) = sut.BuildSql();

        Assert.Empty(errors);
        Assert.Contains("from", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("should_not_be_used", sql, StringComparison.OrdinalIgnoreCase);
    }
}
