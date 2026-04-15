using AkkornStudio.UI.Services.Benchmark;
using Avalonia;
using AkkornStudio.Nodes;
using AkkornStudio.UI.ViewModels;
using Xunit;

namespace AkkornStudio.Tests.Unit.ViewModels;

public class CanvasViewModelTests
{
    [Fact]
    public void IsEmpty_TrueWhenNoNodesOrConnections()
    {
        var vm = new CanvasViewModel();

        Assert.True(vm.IsEmpty);
    }

    [Fact]
    public void IsEmpty_BecomesFalse_WhenNodeIsAdded()
    {
        var vm = new CanvasViewModel();
        var node = new NodeViewModel(NodeDefinitionRegistry.Get(NodeType.TableDefinition), new Point(0, 0));

        vm.Nodes.Add(node);

        Assert.False(vm.IsEmpty);
    }
}

