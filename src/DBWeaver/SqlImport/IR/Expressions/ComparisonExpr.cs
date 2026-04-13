using DBWeaver.SqlImport.Contracts;
using DBWeaver.SqlImport.IR.Metadata;
using DBWeaver.SqlImport.Tracing;

namespace DBWeaver.SqlImport.IR.Expressions;

public sealed record ComparisonExpr(
    string ExprId,
    SourceSpan? SourceSpan,
    SqlImportSemanticType SemanticType,
    SqlResolutionStatus ResolutionStatus,
    TraceMeta TraceMeta,
    SqlIrNodeMetadata NodeMetadata,
    SqlExpression Left,
    ComparisonOperator Operator,
    SqlExpression Right
) : SqlExpression(ExprId, SourceSpan, SemanticType, ResolutionStatus, TraceMeta, NodeMetadata);
