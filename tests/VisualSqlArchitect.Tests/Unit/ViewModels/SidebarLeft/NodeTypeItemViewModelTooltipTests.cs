using VisualSqlArchitect.Nodes;
using VisualSqlArchitect.Nodes.PinTypes;
using VisualSqlArchitect.UI.ViewModels;

namespace VisualSqlArchitect.Tests.Unit.ViewModels.SidebarLeft;

public class NodeTypeItemViewModelTooltipTests
{
    [Fact]
    public void TooltipPins_AreBuiltFromDefinition_ByDirection()
    {
        NodeDefinition definition = NodeDefinitionRegistry.Get(NodeType.Equals);
        var vm = new NodeTypeItemViewModel(definition, "#FFFFFF", _ => { });

        Assert.True(vm.HasInputPins);
        Assert.True(vm.HasOutputPins);
        Assert.Contains(vm.InputPins, p => p.Name == "left");
        Assert.Contains(vm.InputPins, p => p.Name == "right");
        Assert.Contains(vm.OutputPins, p => p.Name == "result");
    }

    [Fact]
    public void TooltipPins_UsePinTypeColor()
    {
        NodeDefinition definition = NodeDefinitionRegistry.Get(NodeType.Equals);
        var vm = new NodeTypeItemViewModel(definition, "#FFFFFF", _ => { });
        string expected = PinTypeRegistry.GetType(PinDataType.ColumnRef).VisualColorHex;

        Assert.All(vm.InputPins, pin => Assert.Equal(expected, pin.Color));
    }

    [Fact]
    public void TooltipPins_MapsShapeForDdlTableDef()
    {
        NodeDefinition definition = NodeDefinitionRegistry.Get(NodeType.CreateTableOutput);
        var vm = new NodeTypeItemViewModel(definition, "#FFFFFF", _ => { });

        NodePinTooltipItemViewModel tableDefPin = Assert.Single(vm.InputPins, p => p.TypeName == nameof(PinDataType.TableDef));
        Assert.Equal("■", tableDefPin.ShapeGlyph);
        Assert.Equal("Rounded Square", tableDefPin.ShapeName);
    }
}
