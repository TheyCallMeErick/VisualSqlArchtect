using AkkornStudio.Metadata;
using AkkornStudio.UI.ViewModels;
using AkkornStudio.UI.ViewModels.Canvas;

namespace AkkornStudio.UI.Services.Canvas.AutoJoin;

internal interface ICanvasAutoJoinSuggestionService
{
    IReadOnlyList<JoinSuggestion> AnalyzeNewTable(string newTableFullName, IReadOnlyCollection<NodeViewModel> nodes);
    IReadOnlyList<JoinSuggestion> AnalyzeAllTables(IReadOnlyCollection<NodeViewModel> nodes);
    IReadOnlyList<JoinSuggestion> AnalyzePair(NodeViewModel left, NodeViewModel right, IReadOnlyCollection<NodeViewModel> nodes);
}





