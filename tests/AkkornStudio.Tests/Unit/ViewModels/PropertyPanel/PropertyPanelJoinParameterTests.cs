using Avalonia;
using AkkornStudio.Nodes;
using AkkornStudio.UI.ViewModels;

namespace AkkornStudio.Tests.Unit.ViewModels.PropertyPanel;

public class PropertyPanelJoinParameterTests
{
    private const string AutoProjectionMarkerParameter = "__akkorn_auto_projection";

    [Fact]
    public void ShowNode_JoinNode_ShowsJoinTypeAndDoesNotShowTypeParameter()
    {
        var canvas = new CanvasViewModel();
        var undo = new UndoRedoStack(canvas);
        var panel = new PropertyPanelViewModel(undo);

        var joinNode = new NodeViewModel(NodeDefinitionRegistry.Get(NodeType.Join), new Point(0, 0));

        panel.ShowNode(joinNode);

        Assert.Contains(panel.Parameters, p => p.Name == "join_type");
        Assert.DoesNotContain(panel.Parameters, p => p.Name == "type");
    }

    [Fact]
    public void CommitDirty_JoinTypeChange_UpdatesJoinTypeOnly()
    {
        var canvas = new CanvasViewModel();
        var undo = new UndoRedoStack(canvas);
        var panel = new PropertyPanelViewModel(undo);

        var joinNode = new NodeViewModel(NodeDefinitionRegistry.Get(NodeType.Join), new Point(0, 0));
        panel.ShowNode(joinNode);

        var joinTypeRow = panel.Parameters.First(p => p.Name == "join_type");
        joinTypeRow.Value = "RIGHT";
        panel.CommitDirty();

        Assert.Equal("RIGHT", joinNode.Parameters["join_type"]);
        Assert.False(joinNode.Parameters.ContainsKey("type"));
    }

    [Fact]
    public void ShowNode_JoinNode_EnablesOpenInErActionWhenBound()
    {
        var canvas = new CanvasViewModel();
        var undo = new UndoRedoStack(canvas);
        var panel = new PropertyPanelViewModel(undo);
        bool invoked = false;
        panel.BindOpenSelectedJoinInErDiagram(() => invoked = true);

        var joinNode = new NodeViewModel(NodeDefinitionRegistry.Get(NodeType.Join), new Point(0, 0));
        panel.ShowNode(joinNode);

        Assert.True(panel.IsSelectedNodeJoinType);
        Assert.True(panel.CanOpenSelectedJoinInErDiagram);
        Assert.True(panel.OpenSelectedJoinInErDiagramCommand.CanExecute(null));

        panel.OpenSelectedJoinInErDiagramCommand.Execute(null);

        Assert.True(invoked);
    }

    [Fact]
    public void ShowNode_NonJoinNode_HidesOpenInErAction()
    {
        var canvas = new CanvasViewModel();
        var undo = new UndoRedoStack(canvas);
        var panel = new PropertyPanelViewModel(undo);
        panel.BindOpenSelectedJoinInErDiagram(() => { });

        var equalsNode = new NodeViewModel(NodeDefinitionRegistry.Get(NodeType.Equals), new Point(0, 0));
        panel.ShowNode(equalsNode);

        Assert.False(panel.IsSelectedNodeJoinType);
        Assert.False(panel.CanOpenSelectedJoinInErDiagram);
        Assert.False(panel.OpenSelectedJoinInErDiagramCommand.CanExecute(null));
    }

    [Fact]
    public void ShowNode_AutoProjectionResultOutput_EnablesRefineActionWhenBound()
    {
        var canvas = new CanvasViewModel();
        var undo = new UndoRedoStack(canvas);
        var panel = new PropertyPanelViewModel(undo);
        bool invoked = false;
        panel.BindRefineAutoProjection(() => invoked = true);

        var resultOutput = new NodeViewModel(NodeDefinitionRegistry.Get(NodeType.ResultOutput), new Point(0, 0));
        resultOutput.Parameters[AutoProjectionMarkerParameter] = "true";
        panel.ShowNode(resultOutput);

        Assert.True(panel.IsSelectedNodeAutoProjectionResultOutput);
        Assert.True(panel.CanRefineAutoProjection);
        Assert.True(panel.RefineAutoProjectionCommand.CanExecute(null));

        panel.RefineAutoProjectionCommand.Execute(null);

        Assert.True(invoked);
    }

    [Fact]
    public void ShowNode_RegularResultOutput_HidesRefineAction()
    {
        var canvas = new CanvasViewModel();
        var undo = new UndoRedoStack(canvas);
        var panel = new PropertyPanelViewModel(undo);
        panel.BindRefineAutoProjection(() => { });

        var resultOutput = new NodeViewModel(NodeDefinitionRegistry.Get(NodeType.ResultOutput), new Point(0, 0));
        panel.ShowNode(resultOutput);

        Assert.False(panel.IsSelectedNodeAutoProjectionResultOutput);
        Assert.False(panel.CanRefineAutoProjection);
        Assert.False(panel.RefineAutoProjectionCommand.CanExecute(null));
    }
}
