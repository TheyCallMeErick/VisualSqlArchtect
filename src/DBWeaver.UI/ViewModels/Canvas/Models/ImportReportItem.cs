using DBWeaver.UI.Services.Theming;
namespace DBWeaver.UI.ViewModels.Canvas;

public sealed class ImportReportItem(
    string label,
    ImportItemStatus status,
    string? note = null,
    string? sourceNodeId = null,
    string? diagnosticCode = null)
{
    public string Label { get; } = label;
    public ImportItemStatus Status { get; } = status;
    public string? Note { get; } =
        status is ImportItemStatus.Partial or ImportItemStatus.Skipped
            ? (string.IsNullOrWhiteSpace(note) ? "Requires manual review." : note)
            : note;
    public string? SourceNodeId { get; } = sourceNodeId;
    public string? DiagnosticCode { get; } = EnsureDiagnosticCode(status, diagnosticCode, label);
    public bool CanFocusNode => !string.IsNullOrWhiteSpace(SourceNodeId);

    public bool IsImported => Status == ImportItemStatus.Imported;
    public bool IsPartial => Status == ImportItemStatus.Partial;
    public bool IsSkipped => Status == ImportItemStatus.Skipped;

    public string StatusIcon =>
        Status switch
        {
            ImportItemStatus.Imported => "✓",
            ImportItemStatus.Partial => "~",
            ImportItemStatus.Skipped => "✗",
            _ => "?",
        };

    public string StatusColor =>
        Status switch
        {
            ImportItemStatus.Imported => UiColorConstants.C_34D399,
            ImportItemStatus.Partial => UiColorConstants.C_FBBF24,
            ImportItemStatus.Skipped => UiColorConstants.C_F87171,
            _ => UiColorConstants.C_8B95A8,
        };

    private static string? EnsureDiagnosticCode(
        ImportItemStatus status,
        string? diagnosticCode,
        string label)
    {
        if (status is ImportItemStatus.Partial or ImportItemStatus.Skipped
            && string.IsNullOrWhiteSpace(diagnosticCode))
        {
            throw new InvalidOperationException(
                $"ImportReportItem with status '{status}' requires DiagnosticCode. Label: '{label}'."
            );
        }
        return diagnosticCode;
    }
}
