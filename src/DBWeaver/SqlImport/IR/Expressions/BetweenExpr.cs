using DBWeaver.SqlImport.Contracts;
using DBWeaver.SqlImport.IR.Metadata;
using DBWeaver.SqlImport.Tracing;

namespace DBWeaver.SqlImport.IR.Expressions;

public sealed record BetweenExpr(
    string ExprId,
    SourceSpan? SourceSpan,
    SqlImportSemanticType SemanticType,
    SqlResolutionStatus ResolutionStatus,
    TraceMeta TraceMeta,
    SqlIrNodeMetadata NodeMetadata,
    SqlExpression Value,
    SqlExpression Low,
    SqlExpression High,
    bool IsNegated
) : SqlExpression(ExprId, SourceSpan, SemanticType, ResolutionStatus, TraceMeta, NodeMetadata);
