using Avalonia;
using AkkornStudio.UI.ViewModels;
using AkkornStudio.UI.ViewModels.Canvas;
using AkkornStudio.Metadata;
using AkkornStudio.Nodes;

namespace AkkornStudio.UI.Services.Canvas.AutoJoin;

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





