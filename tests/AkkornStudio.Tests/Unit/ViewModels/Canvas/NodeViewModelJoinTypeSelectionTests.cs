using AkkornStudio.UI.Services.Canvas.AutoJoin;
using AkkornStudio.UI.Services.Explain;
using Avalonia;
using AkkornStudio.Nodes;
using AkkornStudio.UI.ViewModels;

namespace AkkornStudio.Tests.Unit.ViewModels.Canvas;

public class NodeViewModelJoinTypeSelectionTests
{
    [Fact]
    public void JoinTypeSelection_Setter_UpdatesJoinTypeParameter()
    {
        var node = new NodeViewModel(NodeDefinitionRegistry.Get(NodeType.Join), new Point(0, 0));

        node.JoinTypeSelection = "RIGHT";

        Assert.Equal("RIGHT", node.Parameters["join_type"]);
        Assert.False(node.Parameters.ContainsKey("type"));
    }
}


