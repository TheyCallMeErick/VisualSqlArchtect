using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Threading;
using DBWeaver.Nodes;
using DBWeaver.UI.Services.Search;
using DBWeaver.UI.Serialization;

namespace DBWeaver.UI.ViewModels.Canvas;

/// <summary>
/// Manages the search menu for node/table discovery and spawning.
/// Filters and displays node definitions and database tables for quick access.
/// </summary>
public sealed class SearchMenuViewModel : ViewModelBase
{
    private const int FilterDebounceMs = 80;
    private const int MaxTableResults = 5;
    private const int MaxTotalResults = 12;

    private string _query = "";
    private bool _isVisible;
    private Point _spawnPos;
    private NodeSearchResultViewModel? _selected;
    private CanvasContext _canvasContext = CanvasContext.Query;
    private CancellationTokenSource? _filterCts;
    private int _filterVersion;

    /// <summary>
    /// The search query entered by the user.
    /// Triggers filtering when changed.
    /// </summary>
    public string Query
    {
        get => _query;
        set
        {
            Set(ref _query, value);
            QueueFilterResults();
        }
    }

    /// <summary>
    /// Whether the search menu is currently visible.
    /// </summary>
    public bool IsVisible
    {
        get => _isVisible;
        set => Set(ref _isVisible, value);
    }

    /// <summary>
    /// The canvas position where the menu was spawned.
    /// </summary>
    public Point SpawnPosition
    {
        get => _spawnPos;
        set => Set(ref _spawnPos, value);
    }

    /// <summary>
    /// The currently selected search result.
    /// </summary>
    public NodeSearchResultViewModel? SelectedResult
    {
        get => _selected;
        set => Set(ref _selected, value);
    }

    public CanvasContext CanvasContext
    {
        get => _canvasContext;
        set
        {
            if (!Set(ref _canvasContext, value))
                return;

            // Context switch must clear stale query/state from previous mode.
            _query = "";
            RaisePropertyChanged(nameof(Query));
            QueueFilterResults();
        }
    }

    /// <summary>
    /// Observable collection of filtered search results (nodes and tables).
    /// </summary>
    public ObservableCollection<NodeSearchResultViewModel> Results { get; } = [];

    /// <summary>
    /// Saved snippets visible when the query is empty or matches the snippet name/tags.
    /// </summary>
    public ObservableCollection<SnippetViewModel> Snippets { get; } = [];

    public bool HasSnippets => Snippets.Count > 0;

    private static readonly IReadOnlyList<NodeDefinition> AllDefs = NodeDefinitionRegistry
        .All.OrderBy(d => d.Category)
        .ThenBy(d => d.DisplayName)
        .ToList();

    private readonly List<NodeSearchResultViewModel> _tables = [];
    private readonly List<SavedSnippet> _allSnippets = [];
    private readonly TextSearchService _textSearch = new();

    /// <summary>
    /// Loads available database tables into the search results.
    /// </summary>
    /// <param name="tables">Enumerable of (FullName, Columns) tuples</param>
    public void LoadTables(
        IEnumerable<(string FullName, IReadOnlyList<(string Name, PinDataType Type)> Cols)> tables
    )
    {
        _tables.Clear();
        _tables.AddRange(
            tables.Select(t => NodeSearchResultViewModel.ForTable(t.FullName, t.Cols))
        );
        QueueFilterResults();
    }

    /// <summary>
    /// Reloads snippets from the persistent store and updates the Snippets collection.
    /// Call after adding or removing a snippet.
    /// </summary>
    public void LoadSnippets()
    {
        _allSnippets.Clear();
        _allSnippets.AddRange(SnippetStore.Load());
        QueueFilterResults();
    }

    /// <summary>
    /// Opens the search menu at the specified canvas position.
    /// </summary>
    public void Open(Point pos)
    {
        SpawnPosition = pos;
        LoadSnippets();
        Query = "";
        IsVisible = true;
    }

    /// <summary>
    /// Closes the search menu and clears the query.
    /// </summary>
    public void Close()
    {
        IsVisible = false;
        _filterCts?.Cancel();
        Query = "";
    }

    /// <summary>
    /// Filters results based on current query using debounce + cancellation,
    /// and applies latest-result-wins semantics.
    /// </summary>
    private void QueueFilterResults()
    {
        _filterCts?.Cancel();
        _filterCts?.Dispose();
        _filterCts = new CancellationTokenSource();
        int version = Interlocked.Increment(ref _filterVersion);
        _ = FilterResultsAsync(version, _filterCts.Token);
    }

    private async Task FilterResultsAsync(int version, CancellationToken cancellationToken)
    {
        string query = Query.Trim();
        if (!string.IsNullOrWhiteSpace(query))
            await Task.Delay(FilterDebounceMs, cancellationToken);

        List<NodeSearchResultViewModel> tableSnapshot = _tables.ToList();
        List<SavedSnippet> snippetSnapshot = _allSnippets.ToList();
        List<NodeDefinition> nodeSnapshot = AllDefs.Where(IsDefinitionAllowedInContext).ToList();

        (List<NodeSearchResultViewModel> Results, List<SnippetViewModel> Snippets) payload =
            await Task.Run(() =>
            {
                List<NodeSearchResultViewModel> tables = BuildTableResults(tableSnapshot, query);
                List<NodeSearchResultViewModel> nodes = BuildNodeResults(
                    nodeSnapshot,
                    query,
                    MaxTotalResults - tables.Count);
                List<SnippetViewModel> snippets = BuildSnippetResults(snippetSnapshot, query);
                return (tables.Concat(nodes).ToList(), snippets);
            }, cancellationToken);

        if (version != _filterVersion || cancellationToken.IsCancellationRequested)
            return;

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (version != _filterVersion)
                return;

            Results.Clear();
            foreach (NodeSearchResultViewModel result in payload.Results)
                Results.Add(result);

            Snippets.Clear();
            foreach (SnippetViewModel snippet in payload.Snippets)
                Snippets.Add(snippet);

            RaisePropertyChanged(nameof(HasSnippets));
            SelectedResult = Results.FirstOrDefault();
        });
    }

    private List<NodeSearchResultViewModel> BuildTableResults(
        IReadOnlyList<NodeSearchResultViewModel> tables,
        string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return tables.Take(MaxTableResults).ToList();

        return tables
            .Select(table => (Item: table, Score: _textSearch.Score(query, table.TableFullName, table.Title)))
            .Where(item => item.Score > 0)
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.Item.TableFullName, StringComparer.OrdinalIgnoreCase)
            .Take(MaxTableResults)
            .Select(item => item.Item)
            .ToList();
    }

    private List<NodeSearchResultViewModel> BuildNodeResults(
        IReadOnlyList<NodeDefinition> definitions,
        string query,
        int maxCount)
    {
        if (maxCount <= 0)
            return [];

        if (string.IsNullOrWhiteSpace(query))
            return definitions.Take(maxCount).Select(def => new NodeSearchResultViewModel(def)).ToList();

        return definitions
            .Select(definition =>
                (Definition: definition, Score: _textSearch.Score(query, definition.DisplayName, definition.Category.ToString())))
            .Where(item => item.Score > 0)
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.Definition.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Take(maxCount)
            .Select(item => new NodeSearchResultViewModel(item.Definition))
            .ToList();
    }

    private List<SnippetViewModel> BuildSnippetResults(
        IReadOnlyList<SavedSnippet> snippets,
        string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return snippets.Select(snippet => new SnippetViewModel(snippet)).ToList();

        return snippets
            .Select(snippet => (Snippet: snippet, Score: _textSearch.Score(query, snippet.Name, snippet.Tags)))
            .Where(item => item.Score > 0)
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.Snippet.Name, StringComparer.OrdinalIgnoreCase)
            .Select(item => new SnippetViewModel(item.Snippet))
            .ToList();
    }

    /// <summary>
    /// Selects the next result in the list (wraps around).
    /// </summary>
    public void SelectNext()
    {
        if (Results.Count == 0)
            return;
        SelectedResult = Results[(Results.IndexOf(SelectedResult!) + 1) % Results.Count];
    }

    /// <summary>
    /// Selects the previous result in the list (wraps around).
    /// </summary>
    public void SelectPrev()
    {
        if (Results.Count == 0)
            return;
        SelectedResult = Results[
            (Results.IndexOf(SelectedResult!) - 1 + Results.Count) % Results.Count
        ];
    }

    private bool IsDefinitionAllowedInContext(NodeDefinition def)
    {
        return CanvasContext switch
        {
            CanvasContext.Ddl => def.Category == NodeCategory.Ddl,
            CanvasContext.Query or CanvasContext.ViewSubcanvas => def.Category != NodeCategory.Ddl,
            _ => true,
        };
    }
}
