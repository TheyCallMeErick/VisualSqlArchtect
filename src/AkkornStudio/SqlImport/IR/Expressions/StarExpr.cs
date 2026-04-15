using AkkornStudio.SqlImport.Contracts;
using AkkornStudio.SqlImport.IR.Metadata;
using AkkornStudio.SqlImport.Tracing;

namespace AkkornStudio.SqlImport.IR.Expressions;

public sealed record StarExpr(
    string ExprId,
    SourceSpan? SourceSpan,
    SqlImportSemanticType SemanticType,
    SqlResolutionStatus ResolutionStatus,
    TraceMeta TraceMeta,
    SqlIrNodeMetadata NodeMetadata,
    string? Qualifier
) : SqlExpression(ExprId, SourceSpan, SemanticType, ResolutionStatus, TraceMeta, NodeMetadata);
