using AkkornStudio.UI.Services.Explain;
using AkkornStudio.UI.ViewModels.Canvas;

namespace AkkornStudio.UI.Services.Shell.Models;

public sealed class QueryTabState
{
    public required string FallbackTitle { get; init; }
    public string? SnapshotJson { get; set; }
    public string? CurrentFilePath { get; set; }
    public bool IsDirty { get; set; }
    public List<ExplainHistoryState> ExplainHistory { get; set; } = [];
}
