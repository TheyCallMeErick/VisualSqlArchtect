using AkkornStudio.SqlImport.Contracts;
using AkkornStudio.SqlImport.IR.Metadata;
using AkkornStudio.SqlImport.Tracing;

namespace AkkornStudio.SqlImport.IR.Expressions;

public sealed record FunctionExpr(
    string ExprId,
    SourceSpan? SourceSpan,
    SqlImportSemanticType SemanticType,
    SqlResolutionStatus ResolutionStatus,
    TraceMeta TraceMeta,
    SqlIrNodeMetadata NodeMetadata,
    string Name,
    string? CanonicalName,
    SqlFunctionClassification Classification,
    IReadOnlyList<SqlExpression> Arguments
) : SqlExpression(ExprId, SourceSpan, SemanticType, ResolutionStatus, TraceMeta, NodeMetadata);
