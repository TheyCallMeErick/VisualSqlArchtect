namespace VisualSqlArchitect.UI.ViewModels;

public sealed record StartRecentProjectItem(
    string DisplayName,
    string Provider,
    string LastOpenedLabel,
    string? FilePath = null,
    string? SnapshotSummary = null
);
