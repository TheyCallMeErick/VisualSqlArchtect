using System.Reflection;
using Avalonia;
using VisualSqlArchitect.Metadata;
using VisualSqlArchitect.Nodes;
using VisualSqlArchitect.UI.ViewModels;
using Xunit;

namespace VisualSqlArchitect.Tests.Unit.ViewModels.Canvas;

public class CanvasAutoJoinAcceptanceJoinTypesTests
{
    [Theory]
    [InlineData("INNER")]
    [InlineData("LEFT")]
    [InlineData("RIGHT")]
    [InlineData("FULL")]
    [InlineData("CROSS")]
    public void OnJoinAccepted_CreatesJoinNode_AndWiresPins_ForAllJoinTypes(string joinType)
    {
        var canvas = new CanvasViewModel();
        canvas.Nodes.Clear();
        canvas.Connections.Clear();

        NodeViewModel orders = canvas.SpawnTableNode(
            "public.orders",
            [("id", PinDataType.Number), ("customer_id", PinDataType.Number)],
            new Point(80, 80)
        );
        NodeViewModel customers = canvas.SpawnTableNode(
            "public.customers",
            [("id", PinDataType.Number)],
            new Point(320, 180)
        );

        var suggestion = new JoinSuggestion(
            ExistingTable: "public.orders",
            NewTable: "public.customers",
            JoinType: joinType,
            LeftColumn: "public.orders.customer_id",
            RightColumn: "public.customers.id",
            OnClause: "public.orders.customer_id = public.customers.id",
            Score: 0.99,
            Confidence: JoinConfidence.CatalogDefinedFk,
            Rationale: "test"
        );

        InvokeJoinAccepted(canvas, suggestion);

        NodeViewModel joinNode = Assert.Single(canvas.Nodes, n => n.Type == NodeType.Join);
        Assert.True(joinNode.Parameters.TryGetValue("join_type", out string? actualJoinType));
        Assert.Equal(joinType, actualJoinType);

        ConnectionViewModel leftConn = Assert.Single(canvas.Connections,
            c => c.ToPin?.Owner == joinNode && c.ToPin.Name == "left");
        ConnectionViewModel rightConn = Assert.Single(canvas.Connections,
            c => c.ToPin?.Owner == joinNode && c.ToPin.Name == "right");

        Assert.Equal(orders, leftConn.FromPin.Owner);
        Assert.Equal("customer_id", leftConn.FromPin.Name);
        Assert.Equal(customers, rightConn.FromPin.Owner);
        Assert.Equal("id", rightConn.FromPin.Name);
    }

    [Fact]
    public void OnJoinAccepted_DoesNotCreateDuplicateJoin_ForSamePair()
    {
        var canvas = new CanvasViewModel();
        canvas.Nodes.Clear();
        canvas.Connections.Clear();

        canvas.SpawnTableNode(
            "public.orders",
            [("id", PinDataType.Number), ("customer_id", PinDataType.Number)],
            new Point(80, 80)
        );
        canvas.SpawnTableNode(
            "public.customers",
            [("id", PinDataType.Number)],
            new Point(320, 180)
        );

        var suggestion = new JoinSuggestion(
            ExistingTable: "public.orders",
            NewTable: "public.customers",
            JoinType: "INNER",
            LeftColumn: "public.orders.customer_id",
            RightColumn: "public.customers.id",
            OnClause: "public.orders.customer_id = public.customers.id",
            Score: 0.99,
            Confidence: JoinConfidence.CatalogDefinedFk,
            Rationale: "test"
        );

        InvokeJoinAccepted(canvas, suggestion);
        InvokeJoinAccepted(canvas, suggestion);

        Assert.Single(canvas.Nodes, n => n.Type == NodeType.Join);
    }

    private static void InvokeJoinAccepted(CanvasViewModel canvas, JoinSuggestion suggestion)
    {
        MethodInfo method = typeof(CanvasViewModel)
            .GetMethod("OnJoinAccepted", BindingFlags.Instance | BindingFlags.NonPublic)!;

        method.Invoke(canvas, [null, suggestion]);
    }
}
