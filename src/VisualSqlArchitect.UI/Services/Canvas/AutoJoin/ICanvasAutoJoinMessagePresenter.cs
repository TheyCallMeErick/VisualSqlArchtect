using VisualSqlArchitect.UI.ViewModels.Canvas;
using VisualSqlArchitect.UI.ViewModels;

namespace VisualSqlArchitect.UI.Services.Canvas.AutoJoin;

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





