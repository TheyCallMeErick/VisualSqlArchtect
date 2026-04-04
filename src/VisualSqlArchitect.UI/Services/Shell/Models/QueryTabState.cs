using VisualSqlArchitect.UI.Services.Explain;
using VisualSqlArchitect.UI.ViewModels.Canvas;

namespace VisualSqlArchitect.UI.Services.Shell.Models;

public sealed class QueryTabState
{
    public required string FallbackTitle { get; init; }
    public string? SnapshotJson { get; set; }
    public string? CurrentFilePath { get; set; }
    public bool IsDirty { get; set; }
    public List<ExplainHistoryState> ExplainHistory { get; set; } = [];
}
