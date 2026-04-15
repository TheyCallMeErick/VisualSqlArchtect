using AkkornStudio.UI.ViewModels.Canvas;
using AkkornStudio.UI.ViewModels;

namespace AkkornStudio.UI.Services.Canvas.AutoJoin;

internal interface ICanvasAutoJoinNotifier
{
    void ShowNoSimilarityFound();
    void ShowMultipleCandidatesFound(int suggestionsCount);
    void ShowSuggestionsFound(int suggestionsCount, Action onDetails);
    void ShowSuggestionsUnavailable();
    void ShowAutoJoinApplied(string onClause);
    void ShowManualJoinFailed();
    void ShowManualJoinCreated(string leftRef, string rightRef);
}





