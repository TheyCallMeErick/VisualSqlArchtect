using AkkornStudio.SqlImport.Contracts;
using AkkornStudio.SqlImport.IR.Metadata;
using AkkornStudio.SqlImport.Tracing;

namespace AkkornStudio.SqlImport.IR.Expressions;

public sealed record CaseExpr(
    string ExprId,
    SourceSpan? SourceSpan,
    SqlImportSemanticType SemanticType,
    SqlResolutionStatus ResolutionStatus,
    TraceMeta TraceMeta,
    SqlIrNodeMetadata NodeMetadata,
    IReadOnlyList<CaseWhenBranchExpr> Branches,
    SqlExpression? ElseExpression
) : SqlExpression(ExprId, SourceSpan, SemanticType, ResolutionStatus, TraceMeta, NodeMetadata);
