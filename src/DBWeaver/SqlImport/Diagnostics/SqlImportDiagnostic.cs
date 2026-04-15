using DBWeaver.SqlImport.Contracts;
using DBWeaver.SqlImport.Tracing;

namespace DBWeaver.SqlImport.Diagnostics;

public sealed record SqlImportDiagnostic(
    string Code,
    SqlImportDiagnosticCategory Category,
    SqlImportDiagnosticSeverity Severity,
    string Message,
    SqlImportClause Clause,
    SourceSpan? SourceSpan,
    string? SqlFragment,
    SqlImportDiagnosticAction Action,
    string? RecommendedAction,
    string QueryId,
    string CorrelationId
);
