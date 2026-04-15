using AkkornStudio.UI.Services.Search;

namespace AkkornStudio.UI.Services.CommandPalette;

internal static class FuzzyScorer
{
    private static readonly TextSearchService Search = new();

    public static int Score(PaletteCommandItem item, string query)
    {
        if (string.IsNullOrEmpty(query))
            return 1;

        return Search.Score(query, item.Name, item.Description, item.Tags, item.Shortcut);
    }
}

