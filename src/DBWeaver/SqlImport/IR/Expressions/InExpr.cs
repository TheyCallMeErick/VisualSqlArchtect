using DBWeaver.SqlImport.Contracts;
using DBWeaver.SqlImport.IR.Metadata;
using DBWeaver.SqlImport.Tracing;

namespace DBWeaver.SqlImport.IR.Expressions;

public sealed record InExpr(
    string ExprId,
    SourceSpan? SourceSpan,
    SqlImportSemanticType SemanticType,
    SqlResolutionStatus ResolutionStatus,
    TraceMeta TraceMeta,
    SqlIrNodeMetadata NodeMetadata,
    SqlExpression Value,
    IReadOnlyList<SqlExpression> Values,
    QueryExpr? Subquery,
    bool IsNegated
) : SqlExpression(ExprId, SourceSpan, SemanticType, ResolutionStatus, TraceMeta, NodeMetadata);
