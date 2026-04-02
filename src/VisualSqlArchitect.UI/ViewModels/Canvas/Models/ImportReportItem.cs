namespace VisualSqlArchitect.UI.ViewModels.Canvas;

public sealed class ImportReportItem(
    string label,
    EImportItemStatus status,
    string? note = null,
    string? sourceNodeId = null)
{
    public string Label { get; } = label;
    public EImportItemStatus Status { get; } = status;
    public string? Note { get; } =
        status is EImportItemStatus.Partial or EImportItemStatus.Skipped
            ? (string.IsNullOrWhiteSpace(note) ? "Requires manual review." : note)
            : note;
    public string? SourceNodeId { get; } = sourceNodeId;
    public bool CanFocusNode => !string.IsNullOrWhiteSpace(SourceNodeId);

    public bool IsImported => Status == EImportItemStatus.Imported;
    public bool IsPartial => Status == EImportItemStatus.Partial;
    public bool IsSkipped => Status == EImportItemStatus.Skipped;

    public string StatusIcon =>
        Status switch
        {
            EImportItemStatus.Imported => "✓",
            EImportItemStatus.Partial => "~",
            EImportItemStatus.Skipped => "✗",
            _ => "?",
        };

    public string StatusColor =>
        Status switch
        {
            EImportItemStatus.Imported => "#34D399",
            EImportItemStatus.Partial => "#FBBF24",
            EImportItemStatus.Skipped => "#F87171",
            _ => "#8B95A8",
        };
}
