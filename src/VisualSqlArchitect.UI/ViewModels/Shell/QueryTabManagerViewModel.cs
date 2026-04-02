using System.Collections.ObjectModel;

namespace VisualSqlArchitect.UI.ViewModels;

/// <summary>
/// Holds query tab session state independent from the window code-behind.
/// </summary>
public sealed class QueryTabManagerViewModel : ViewModelBase
{
    private int _activeTabIndex;

    public ObservableCollection<QueryTabState> Tabs { get; } = [];

    public int ActiveTabIndex
    {
        get => _activeTabIndex;
        private set => Set(ref _activeTabIndex, value);
    }

    public bool IsRestoringTab { get; set; }

    public void Initialize(string firstSnapshotJson, string? currentFilePath, bool isDirty)
    {
        Tabs.Clear();
        Tabs.Add(new QueryTabState
        {
            FallbackTitle = "Consulta 1",
            SnapshotJson = firstSnapshotJson,
            CurrentFilePath = currentFilePath,
            IsDirty = isDirty,
        });

        ActiveTabIndex = 0;
    }

    public void CaptureActive(string snapshotJson, string? currentFilePath, bool isDirty)
    {
        if (!TryGetActive(out QueryTabState active))
            return;

        active.SnapshotJson = snapshotJson;
        active.CurrentFilePath = currentFilePath;
        active.IsDirty = isDirty;
    }

    public void SyncActiveMetadata(string? currentFilePath, bool isDirty)
    {
        if (!TryGetActive(out QueryTabState active))
            return;

        active.CurrentFilePath = currentFilePath;
        active.IsDirty = isDirty;
    }

    public QueryTabState? GetTab(int tabIndex)
    {
        if (tabIndex < 0 || tabIndex >= Tabs.Count)
            return null;

        return Tabs[tabIndex];
    }

    public bool TryActivate(int tabIndex)
    {
        if (tabIndex < 0 || tabIndex >= Tabs.Count || tabIndex == ActiveTabIndex)
            return false;

        ActiveTabIndex = tabIndex;
        return true;
    }

    public void ResetActive(string snapshotJson)
    {
        if (!TryGetActive(out QueryTabState active))
            return;

        active.SnapshotJson = snapshotJson;
        active.CurrentFilePath = null;
        active.IsDirty = false;
    }

    public void AddNewTab(string snapshotJson)
    {
        int tabNumber = Tabs.Count + 1;
        Tabs.Add(new QueryTabState
        {
            FallbackTitle = $"Consulta {tabNumber}",
            SnapshotJson = snapshotJson,
            CurrentFilePath = null,
            IsDirty = false,
        });

        ActiveTabIndex = Tabs.Count - 1;
    }

    private bool TryGetActive(out QueryTabState tab)
    {
        QueryTabState? found = GetTab(ActiveTabIndex);
        if (found is null)
        {
            tab = null!;
            return false;
        }

        tab = found;
        return true;
    }
}
