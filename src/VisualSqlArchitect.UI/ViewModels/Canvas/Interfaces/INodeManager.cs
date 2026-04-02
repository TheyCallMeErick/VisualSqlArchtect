using Avalonia;
using VisualSqlArchitect.Nodes;

namespace VisualSqlArchitect.UI.ViewModels.Canvas;

public interface INodeManager
{
    NodeViewModel SpawnNode(NodeDefinition def, Point pos);

    NodeViewModel SpawnTableNode(
        string table,
        IEnumerable<(string n, PinDataType t)> cols,
        Point pos);

    void DeleteSelected();

    void CleanupOrphans();

    void SpawnDemoNodes(UndoRedoStack undoRedo);
}
