namespace AkkornStudio.UI.Services.Search;

public sealed class TextSearchService
{
    private readonly SubsequenceFuzzyScorer _fuzzyScorer;

    public TextSearchService(SubsequenceFuzzyScorer? fuzzyScorer = null)
    {
        _fuzzyScorer = fuzzyScorer ?? new SubsequenceFuzzyScorer();
    }

    public bool Matches(string query, params string?[] candidates) => Score(query, candidates) > 0;

    public bool MatchesContainsAllTokens(string query, params string?[] candidates)
    {
        string normalizedQuery = query?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalizedQuery))
            return true;

        string[] tokens = normalizedQuery.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (tokens.Length == 0)
            return true;

        foreach (string token in tokens)
        {
            bool tokenMatched = candidates.Any(candidate =>
                !string.IsNullOrWhiteSpace(candidate)
                && candidate.Contains(token, StringComparison.OrdinalIgnoreCase));

            if (!tokenMatched)
                return false;
        }

        return true;
    }

    public int Score(string query, params string?[] candidates)
    {
        string normalizedQuery = query?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalizedQuery))
            return 1;

        string[] tokens = normalizedQuery.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (tokens.Length == 0)
            return 1;

        int total = 0;
        foreach (string token in tokens)
        {
            int tokenBest = 0;
            foreach (string? candidate in candidates)
            {
                int candidateScore = ScoreSingle(token, candidate);
                if (candidateScore > tokenBest)
                    tokenBest = candidateScore;
            }

            if (tokenBest <= 0)
                return 0;

            total += tokenBest;
        }

        return total;
    }

    private int ScoreSingle(string token, string? candidate)
    {
        string value = candidate?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(value))
            return 0;

        if (string.Equals(value, token, StringComparison.OrdinalIgnoreCase))
            return 450;

        if (value.StartsWith(token, StringComparison.OrdinalIgnoreCase))
            return 300;

        if (value.Contains(token, StringComparison.OrdinalIgnoreCase))
            return 200;

        int fuzzy = _fuzzyScorer.Score(token, value);
        if (fuzzy <= 0)
            return 0;

        return 100 + fuzzy;
    }
}
