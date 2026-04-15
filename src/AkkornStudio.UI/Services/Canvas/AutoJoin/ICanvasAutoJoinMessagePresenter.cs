using AkkornStudio.UI.ViewModels.Canvas;
using AkkornStudio.UI.ViewModels;

namespace AkkornStudio.UI.Services.Canvas.AutoJoin;

internal interface ICanvasAutoJoinMessagePresenter
{
    AutoJoinToastMessage NoSimilarityFound();
    AutoJoinToastMessage MultipleCandidatesFound(int suggestionsCount);
    AutoJoinToastMessage SuggestionsFound(int suggestionsCount);
    AutoJoinToastMessage SuggestionsUnavailable();
    AutoJoinToastMessage AutoJoinApplied(string onClause);
    AutoJoinToastMessage ManualJoinFailed();
    AutoJoinToastMessage ManualJoinCreated(string leftRef, string rightRef);
}





