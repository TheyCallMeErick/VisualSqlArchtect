namespace VisualSqlArchitect.UI.ViewModels;

public sealed class QueryTabState
{
    public required string FallbackTitle { get; init; }
    public string? SnapshotJson { get; set; }
    public string? CurrentFilePath { get; set; }
    public bool IsDirty { get; set; }
}
