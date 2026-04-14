using Avalonia;
using DBWeaver.Nodes;
using DBWeaver.UI.Services.Validation;
using DBWeaver.UI.ViewModels;

namespace DBWeaver.Tests.Unit.Validation;

public class OrphanNodeDetectorTests
{
    [Fact]
    public void DetectOrphanIds_JoinWithParameterModeWithoutOutputConnection_IsNotOrphan()
    {
        var canvas = new CanvasViewModel();
        NodeViewModel table = new("public.orders", [("id", PinDataType.Number)], new Point(0, 0));
        NodeViewModel result = new(NodeDefinitionRegistry.Get(NodeType.ResultOutput), new Point(300, 0));
        NodeViewModel join = new(NodeDefinitionRegistry.Get(NodeType.Join), new Point(180, 0));

        join.Parameters["right_source"] = "public.customers";
        join.Parameters["left_expr"] = "public.orders.customer_id";
        join.Parameters["right_expr"] = "public.customers.id";

        canvas.Nodes.Add(table);
        canvas.Nodes.Add(result);
        canvas.Nodes.Add(join);

        Wire(table, "*", result, "columns", canvas);

        IReadOnlySet<string> orphanIds = OrphanNodeDetector.DetectOrphanIds(canvas);
        Assert.DoesNotContain(join.Id, orphanIds);
    }

    [Fact]
    public void DetectOrphanIds_JoinWithoutSemanticContribution_RemainsOrphan()
    {
        var canvas = new CanvasViewModel();
        NodeViewModel table = new("public.orders", [("id", PinDataType.Number)], new Point(0, 0));
        NodeViewModel result = new(NodeDefinitionRegistry.Get(NodeType.ResultOutput), new Point(300, 0));
        NodeViewModel join = new(NodeDefinitionRegistry.Get(NodeType.Join), new Point(180, 0));

        canvas.Nodes.Add(table);
        canvas.Nodes.Add(result);
        canvas.Nodes.Add(join);

        Wire(table, "*", result, "columns", canvas);

        IReadOnlySet<string> orphanIds = OrphanNodeDetector.DetectOrphanIds(canvas);
        Assert.Contains(join.Id, orphanIds);
    }

    [Fact]
    public void DetectOrphanIds_TableConnectedThroughSemanticallyActiveJoinToReachableFlow_IsNotOrphan()
    {
        var canvas = new CanvasViewModel();
        NodeViewModel tableA = new("public.orders", [("id", PinDataType.Number), ("customer_id", PinDataType.Number)], new Point(0, 0));
        NodeViewModel tableB = new("public.customers", [("id", PinDataType.Number)], new Point(220, 0));
        NodeViewModel result = new(NodeDefinitionRegistry.Get(NodeType.ResultOutput), new Point(520, 0));
        NodeViewModel join = new(NodeDefinitionRegistry.Get(NodeType.Join), new Point(360, 0));

        join.Parameters["right_source"] = "public.customers";
        join.Parameters["left_expr"] = "public.orders.customer_id";
        join.Parameters["right_expr"] = "public.customers.id";

        canvas.Nodes.Add(tableA);
        canvas.Nodes.Add(tableB);
        canvas.Nodes.Add(join);
        canvas.Nodes.Add(result);

        Wire(tableA, "id", result, "column", canvas);
        Wire(tableA, "customer_id", join, "left", canvas);
        Wire(tableB, "id", join, "right", canvas);

        IReadOnlySet<string> orphanIds = OrphanNodeDetector.DetectOrphanIds(canvas);
        Assert.DoesNotContain(tableB.Id, orphanIds);
    }

    private static void Wire(
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

