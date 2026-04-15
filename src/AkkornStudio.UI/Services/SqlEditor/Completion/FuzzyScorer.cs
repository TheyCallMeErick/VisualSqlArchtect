using AkkornStudio.UI.Services.Search;

namespace AkkornStudio.UI.Services.SqlEditor;

public sealed class FuzzyScorer
{
    private readonly SubsequenceFuzzyScorer _inner = new();

    public int Score(string pattern, string candidate)
        => _inner.Score(pattern, candidate);
}
