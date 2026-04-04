
using Avalonia;
using VisualSqlArchitect.Core;
using VisualSqlArchitect.Nodes;
using VisualSqlArchitect.Registry;
using VisualSqlArchitect.UI.ViewModels;
using VisualSqlArchitect.UI.Services.QueryPreview.Models;
using VisualSqlArchitect.UI.Services.QueryPreview;
using static VisualSqlArchitect.Tests.Unit.ViewModels.QueryPreview.QueryPreviewTestNodeFactory;

namespace VisualSqlArchitect.Tests.Unit.ViewModels.QueryPreview;
public class QueryGraphBuilderDiagnosticsTests
{
    [Fact]
    public void BuildSqlWithDiagnostics_PreservesLegacyMessages()
    {
        var canvas = new CanvasViewModel();
        canvas.Nodes.Clear();
        canvas.Connections.Clear();

        NodeViewModel orders = Table("orders", "id");
        NodeViewModel columnList = Node(NodeType.ColumnList);
        NodeViewModel result = Node(NodeType.ResultOutput);
        result.Parameters["query_hints"] = "DROP TABLE x";

        Connect(canvas, orders, "id", columnList, "columns");
        Connect(canvas, columnList, "result", result, "columns");

        canvas.Nodes.Add(orders);
        canvas.Nodes.Add(columnList);
        canvas.Nodes.Add(result);

        var sut = new QueryGraphBuilder(canvas, DatabaseProvider.SQLite);

        (string sqlWithLegacy, List<string> errors) = sut.BuildSql();
        (string sqlWithDiagnostics, List<PreviewDiagnostic> diagnostics) = sut.BuildSqlWithDiagnostics();

        Assert.Equal(sqlWithLegacy, sqlWithDiagnostics);
        Assert.Equal(errors, diagnostics.Select(d => d.Message).ToList());
        Assert.Contains(diagnostics, d => d.Category == EPreviewDiagnosticCategory.General);
        Assert.Contains(diagnostics, d => d.Code.EndsWith("GEN-001", StringComparison.Ordinal));
    }

    [Fact]
    public void BuildSqlWithDiagnostics_AliasAmbiguity_ContainsNodeContext()
    {
        var canvas = new CanvasViewModel();
        canvas.Nodes.Clear();
        canvas.Connections.Clear();

        NodeViewModel ordersA = Table("orders", "id");
        NodeViewModel ordersB = Table("orders_archive", "id");
        ordersA.Alias = "o";
        ordersB.Alias = "o";

        NodeViewModel columnList = Node(NodeType.ColumnList);
        NodeViewModel result = Node(NodeType.ResultOutput);

        Connect(canvas, ordersA, "id", columnList, "columns");
        Connect(canvas, ordersB, "id", columnList, "columns");
        Connect(canvas, columnList, "result", result, "columns");

        canvas.Nodes.Add(ordersA);
        canvas.Nodes.Add(ordersB);
        canvas.Nodes.Add(columnList);
        canvas.Nodes.Add(result);

        var sut = new QueryGraphBuilder(canvas, DatabaseProvider.Postgres);

        (_, List<PreviewDiagnostic> diagnostics) = sut.BuildSqlWithDiagnostics();

        PreviewDiagnostic ambiguity = Assert.Single(
            diagnostics,
            d => d.Message.Contains("alias ambiguity", StringComparison.OrdinalIgnoreCase)
        );

        Assert.False(string.IsNullOrWhiteSpace(ambiguity.NodeId));
        Assert.Contains(ambiguity.NodeId, new[] { ordersA.Id, ordersB.Id });
    }
}





