using System.Collections.ObjectModel;

namespace VisualSqlArchitect.UI.ViewModels;

// ── ViewModel ────────────────────────────────────────────────────────────────

public sealed class CommandPaletteViewModel : ViewModelBase
{
    private bool _isVisible;
    private string _query = "";
    private int _selectedIndex = 0;

    private readonly List<PaletteCommandItem> _all = [];

    public ObservableCollection<PaletteCommandItem> Results { get; } = [];

    public bool IsVisible
    {
        get => _isVisible;
        set => Set(ref _isVisible, value);
    }

    public string Query
    {
        get => _query;
        set
        {
            Set(ref _query, value);
            Filter();
        }
    }

    public int SelectedIndex
    {
        get => _selectedIndex;
        set => Set(ref _selectedIndex, value);
    }

    // ── Registration ─────────────────────────────────────────────────────────

    public void RegisterCommands(IEnumerable<PaletteCommandItem> commands) =>
        _all.AddRange(commands);

    // ── Lifecycle ────────────────────────────────────────────────────────────

    public void Open()
    {
        Query = "";
        SelectedIndex = 0;
        Filter();
        IsVisible = true;
    }

    public void Close()
    {
        IsVisible = false;
        Query = "";
    }

    // ── Navigation ───────────────────────────────────────────────────────────

    public void SelectNext()
    {
        if (Results.Count > 0)
            SelectedIndex = (SelectedIndex + 1) % Results.Count;
    }

    public void SelectPrev()
    {
        if (Results.Count > 0)
            SelectedIndex = (SelectedIndex - 1 + Results.Count) % Results.Count;
    }

    public void ExecuteSelected()
    {
        if (SelectedIndex < 0 || SelectedIndex >= Results.Count)
            return;
        PaletteCommandItem cmd = Results[SelectedIndex];
        Close();
        cmd.Execute();
    }

    // ── Fuzzy filtering ───────────────────────────────────────────────────────

    private void Filter()
    {
        Results.Clear();
        SelectedIndex = 0;
        string q = _query.Trim();

        IOrderedEnumerable<(PaletteCommandItem Command, int Score)> scored = _all.Select(c =>
                (Command: c, Score: FuzzyScorer.Score(c, q))
            )
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Command.Name);

        foreach ((PaletteCommandItem cmd, int _) in scored)
            Results.Add(cmd);
    }
}
