namespace VisualSqlArchitect.UI.Services.QueryPreview.Models;

public sealed class PreviewDiagnostic
{
    public PreviewDiagnostic(
        EPreviewDiagnosticSeverity severity,
        EPreviewDiagnosticCategory category,
        string code,
        string message,
        string? nodeId = null,
        string? pinName = null)
    {
        Severity = severity;
        Category = category;
        Code = code;
        Message = message;
        NodeId = nodeId;
        PinName = pinName;
    }

    public EPreviewDiagnosticSeverity Severity { get; }

    public EPreviewDiagnosticCategory Category { get; }

    public string Code { get; }

    public string Message { get; }

    public string? NodeId { get; }

    public string? PinName { get; }
}
