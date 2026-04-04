using VisualSqlArchitect.Metadata;
using VisualSqlArchitect.UI.ViewModels;
using VisualSqlArchitect.UI.ViewModels.Canvas;

namespace VisualSqlArchitect.UI.Services.Canvas.AutoJoin;

internal interface ICanvasAutoJoinSuggestionService
{
    IReadOnlyList<JoinSuggestion> AnalyzeNewTable(string newTableFullName, IReadOnlyCollection<NodeViewModel> nodes);
    IReadOnlyList<JoinSuggestion> AnalyzeAllTables(IReadOnlyCollection<NodeViewModel> nodes);
    IReadOnlyList<JoinSuggestion> AnalyzePair(NodeViewModel left, NodeViewModel right, IReadOnlyCollection<NodeViewModel> nodes);
}





