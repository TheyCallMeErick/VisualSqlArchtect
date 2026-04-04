using Avalonia;
using VisualSqlArchitect.UI.ViewModels;
using VisualSqlArchitect.UI.ViewModels.Canvas;
using VisualSqlArchitect.Metadata;
using VisualSqlArchitect.Nodes;

namespace VisualSqlArchitect.UI.Services.Canvas.AutoJoin;

internal interface ICanvasAutoJoinApplicationService
{
    bool TryApplySuggestion(
        JoinSuggestion suggestion,
        IReadOnlyCollection<NodeViewModel> nodes,
        IReadOnlyCollection<ConnectionViewModel> connections,
        Func<NodeDefinition, Point, NodeViewModel> spawnNode,
        Action<PinViewModel, PinViewModel> connectPins);

    bool TryCreateManualJoin(
        NodeViewModel leftTable,
        string leftColumn,
        NodeViewModel rightTable,
        string rightColumn,
        string? joinType,
        IReadOnlyCollection<NodeViewModel> nodes,
        IReadOnlyCollection<ConnectionViewModel> connections,
        Func<NodeDefinition, Point, NodeViewModel> spawnNode,
        Action<PinViewModel, PinViewModel> connectPins);
}





