using DBWeaver.UI.ViewModels.Canvas;
using DBWeaver.SqlImport.Diagnostics;

namespace DBWeaver.UI.Services.SqlImport;

public static class SqlImportReportFactory
{
    public static ImportReportItem Diagnostic(
        string code,
        string label,
        ImportItemStatus status,
        string? note = null,
        string? sourceNodeId = null
    )
    {
        return new ImportReportItem(
            SqlImportReportDiagnostics.WithCode(code, label),
            status,
            note,
            sourceNodeId,
            code
        );
    }

    public static ImportReportItem Partial(
        string code,
        string label,
        string? note = null,
        string? sourceNodeId = null
    )
    {
        return Diagnostic(code, label, ImportItemStatus.Partial, note, sourceNodeId);
    }

    public static ImportReportItem Skipped(
        string code,
        string label,
        string? note = null,
        string? sourceNodeId = null
    )
    {
        return Diagnostic(code, label, ImportItemStatus.Skipped, note, sourceNodeId);
    }

    public static ImportReportItem WherePartial(string label, string? sourceNodeId = null)
    {
        return Partial(
            SqlImportDiagnosticCodes.AstUnsupported,
            label,
            SqlImportDiagnosticMessages.WherePartialReportNote,
            sourceNodeId
        );
    }

    public static ImportReportItem WhereUnsupported(string label)
    {
        return Partial(
            SqlImportDiagnosticCodes.AstUnsupported,
            label,
            SqlImportDiagnosticMessages.WhereUnsupportedReportNote
        );
    }

    public static ImportReportItem OrderByPartial(string label, string? sourceNodeId = null)
    {
        return Partial(
            SqlImportDiagnosticCodes.ColumnUnresolved,
            label,
            SqlImportDiagnosticMessages.OrderByPartialReportNote,
            sourceNodeId
        );
    }

    public static ImportReportItem OrderByUnsupported(string label)
    {
        return Skipped(
            SqlImportDiagnosticCodes.AstUnsupported,
            label,
            SqlImportDiagnosticMessages.OrderByUnsupportedReportNote
        );
    }

    public static ImportReportItem GroupByPartial(string label, string? sourceNodeId = null)
    {
        return Partial(
            SqlImportDiagnosticCodes.ColumnUnresolved,
            label,
            SqlImportDiagnosticMessages.GroupByPartialReportNote,
            sourceNodeId
        );
    }

    public static ImportReportItem GroupByUnsupported(string label)
    {
        return Skipped(
            SqlImportDiagnosticCodes.AstUnsupported,
            label,
            SqlImportDiagnosticMessages.GroupByUnsupportedReportNote
        );
    }

    public static ImportReportItem GroupByConflict(string label)
    {
        return Partial(
            SqlImportDiagnosticCodes.AstUnsupported,
            label,
            SqlImportDiagnosticMessages.GroupByConflictReportNote
        );
    }

    public static ImportReportItem HavingUnsupported(string label, string? sourceNodeId = null)
    {
        return Partial(
            SqlImportDiagnosticCodes.AstUnsupported,
            label,
            SqlImportDiagnosticMessages.HavingUnsupportedReportNote,
            sourceNodeId
        );
    }
}
