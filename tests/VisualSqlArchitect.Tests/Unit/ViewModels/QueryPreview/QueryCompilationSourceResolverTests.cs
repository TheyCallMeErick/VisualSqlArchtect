using DBWeaver.Nodes;
using DBWeaver.UI.ViewModels;
using DBWeaver.UI.Services.QueryPreview;

namespace DBWeaver.Tests.Unit.ViewModels.QueryPreview;

public class QueryCompilationSourceResolverTests
{
    [Fact]
    public void ResolveFromTable_WhenTableExists_UsesTableName()
    {
        var canvas = new CanvasViewModel();
        var resolver = new QueryCompilationSourceResolver(canvas, (_, _) => null);
        NodeViewModel table = QueryPreviewTestNodeFactory.Table("public.orders", "id");

        (string fromTable, string? warning) = resolver.ResolveFromTable(
            [table],
            [],
            [],
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));

        Assert.Equal("public.orders", fromTable);
        Assert.Null(warning);
    }

    [Fact]
    public void ResolveFromTable_WhenCteSourceExists_UsesResolvedCteReference()
    {
        var canvas = new CanvasViewModel();
        var resolver = new QueryCompilationSourceResolver(canvas, (_, _) => "orders_cte AS oc");
        NodeViewModel cteSource = QueryPreviewTestNodeFactory.Node(NodeType.CteSource);

        (string fromTable, string? warning) = resolver.ResolveFromTable(
            [],
            [cteSource],
            [],
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));

        Assert.Equal("orders_cte AS oc", fromTable);
        Assert.Null(warning);
    }

    [Fact]
    public void ResolveFromTable_WhenSubqueryAliasHasSpaces_ReturnsDefaultAliasWarning()
    {
        var canvas = new CanvasViewModel();
        var resolver = new QueryCompilationSourceResolver(canvas, (_, _) => null);
        NodeViewModel subquery = QueryPreviewTestNodeFactory.Node(NodeType.Subquery);
        subquery.Parameters["query"] = "SELECT 1";
        subquery.Parameters["alias"] = "bad alias";

        (string fromTable, string? warning) = resolver.ResolveFromTable(
            [],
            [],
            [subquery],
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));

        Assert.Equal("(SELECT 1) subq", fromTable);
        Assert.Contains("cannot contain spaces", warning, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ResolveFromTable_WhenUpstreamTableMarkedAsPrimary_UsesMarkedNodeAsFrom()
    {
        var canvas = new CanvasViewModel();
        var resolver = new QueryCompilationSourceResolver(canvas, (_, _) => null);

        NodeViewModel tableA = QueryPreviewTestNodeFactory.Table("public.orders", "id", "customer_id");
        NodeViewModel tableB = QueryPreviewTestNodeFactory.Table("public.customers", "id");
        NodeViewModel result = QueryPreviewTestNodeFactory.Node(NodeType.ResultOutput);

        canvas.Nodes.Add(tableA);
        canvas.Nodes.Add(tableB);
        canvas.Nodes.Add(result);

        Connect(tableA, "id", result, "column", canvas);
        Connect(tableA, "customer_id", result, "column", canvas);
        Connect(tableB, "id", result, "column", canvas);

        tableA.IsPrimaryFromSource = false;
        tableB.IsPrimaryFromSource = true;

        (string fromTable, string? warning) = resolver.ResolveFromTable(
            [tableA, tableB],
            [],
            [],
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));

        Assert.Equal("public.customers", fromTable);
        Assert.Null(warning);
    }

    private static void Connect(
        NodeViewModel fromNode,
        string fromPinName,
        NodeViewModel toNode,
        string toPinName,
        CanvasViewModel canvas)
    {
        PinViewModel fromPin = fromNode.OutputPins.First(pin =>
            string.Equals(pin.Name, fromPinName, StringComparison.OrdinalIgnoreCase));
        PinViewModel toPin = toNode.InputPins.First(pin =>
            string.Equals(pin.Name, toPinName, StringComparison.OrdinalIgnoreCase));

        var connection = new ConnectionViewModel(fromPin, default, default)
        {
            ToPin = toPin,
        };

        fromPin.IsConnected = true;
        toPin.IsConnected = true;
        canvas.Connections.Add(connection);
    }
}

