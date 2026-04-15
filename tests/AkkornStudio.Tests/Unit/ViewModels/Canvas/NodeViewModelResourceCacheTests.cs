using AkkornStudio.UI.Services.Canvas.AutoJoin;
using AkkornStudio.UI.Services.Explain;
using Avalonia;
using AkkornStudio.Nodes;
using AkkornStudio.UI.ViewModels;
using Xunit;

namespace AkkornStudio.Tests.Unit.ViewModels.Canvas;

public class NodeViewModelResourceCacheTests
{
    [Fact]
    public void HeaderGradient_IsReusedAcrossReads()
    {
        var node = new NodeViewModel("public.orders", [("id", PinDataType.Number)], new Point(0, 0));

        var g1 = node.HeaderGradient;
        var g2 = node.HeaderGradient;

        Assert.Same(g1, g2);
    }

    [Fact]
    public void HeaderColor_IsStableAcrossReads()
    {
        var node = new NodeViewModel("public.orders", [("id", PinDataType.Number)], new Point(0, 0));

        var c1 = node.HeaderColor;
        var c2 = node.HeaderColor;

        Assert.Equal(c1, c2);
    }

    [Fact]
    public void HeaderColorLight_IsStableAcrossReads()
    {
        var node = new NodeViewModel("public.orders", [("id", PinDataType.Number)], new Point(0, 0));

        var c1 = node.HeaderColorLight;
        var c2 = node.HeaderColorLight;

        Assert.Equal(c1, c2);
    }

    [Fact]
    public void NodeBorderBrush_DefaultState_IsReusedAcrossReads()
    {
        var node = new NodeViewModel("public.orders", [("id", PinDataType.Number)], new Point(0, 0));

        var b1 = node.NodeBorderBrush;
        var b2 = node.NodeBorderBrush;

        Assert.Same(b1, b2);
    }

    [Fact]
    public void NodeBorderBrush_UsesHighlightBrush_WhenIsHighlighted()
    {
        var node = new NodeViewModel("public.orders", [("id", PinDataType.Number)], new Point(0, 0));
        var defaultBrush = node.NodeBorderBrush;

        node.IsHighlighted = true;

        Assert.NotSame(defaultBrush, node.NodeBorderBrush);
    }

    [Fact]
    public void HeaderColor_ForImportedFlowCategories_IsNotDataSourceFallback()
    {
        var dataSourceNode = new NodeViewModel("public.orders", [("id", PinDataType.Number)], new Point(0, 0));
        var outputNode = new NodeViewModel(NodeDefinitionRegistry.Get(NodeType.ResultOutput), new Point(0, 0));
        var resultModifierNode = new NodeViewModel(NodeDefinitionRegistry.Get(NodeType.Top), new Point(0, 0));
        var literalNode = new NodeViewModel(NodeDefinitionRegistry.Get(NodeType.ValueString), new Point(0, 0));

        Assert.NotEqual(dataSourceNode.HeaderColor, outputNode.HeaderColor);
        Assert.NotEqual(dataSourceNode.HeaderColor, resultModifierNode.HeaderColor);
        Assert.NotEqual(dataSourceNode.HeaderColor, literalNode.HeaderColor);
    }
}


