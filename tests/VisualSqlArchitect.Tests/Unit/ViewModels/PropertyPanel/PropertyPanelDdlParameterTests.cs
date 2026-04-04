using Avalonia;
using VisualSqlArchitect.Nodes;
using VisualSqlArchitect.UI.ViewModels;
using Xunit;

namespace VisualSqlArchitect.Tests.Unit.ViewModels.PropertyPanel;

public class PropertyPanelDdlParameterTests
{
    [Fact]
    public void ShowNode_TableDefinition_ExposesTableParameters()
    {
        var panel = CreatePanel();
        var node = new NodeViewModel(NodeDefinitionRegistry.Get(NodeType.TableDefinition), new Point(0, 0));

        panel.ShowNode(node);

        Assert.Contains(panel.Parameters, p => p.Name == "SchemaName");
        Assert.Contains(panel.Parameters, p => p.Name == "TableName");
        Assert.Contains(panel.Parameters, p => p.Name == "IfNotExists");
    }

    [Fact]
    public void ShowNode_ColumnDefinition_ExposesColumnParameters()
    {
        var panel = CreatePanel();
        var node = new NodeViewModel(NodeDefinitionRegistry.Get(NodeType.ColumnDefinition), new Point(0, 0));

        panel.ShowNode(node);

        Assert.Contains(panel.Parameters, p => p.Name == "ColumnName");
        Assert.Contains(panel.Parameters, p => p.Name == "DataType");
        Assert.Contains(panel.Parameters, p => p.Name == "IsNullable");
    }

    private static PropertyPanelViewModel CreatePanel()
    {
        var canvas = new CanvasViewModel();
        var undo = new UndoRedoStack(canvas);
        return new PropertyPanelViewModel(undo);
    }
}
