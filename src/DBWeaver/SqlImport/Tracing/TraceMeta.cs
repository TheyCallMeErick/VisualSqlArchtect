namespace DBWeaver.SqlImport.Tracing;

public sealed record TraceMeta(
    string QueryId,
    string? ExprId,
    string? CorrelationId,
    SourceSpan? SourceSpan
);
