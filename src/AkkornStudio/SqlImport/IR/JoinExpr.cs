using AkkornStudio.SqlImport.Contracts;
using AkkornStudio.SqlImport.IR.Expressions;
using AkkornStudio.SqlImport.IR.Sources;
using AkkornStudio.SqlImport.IR.Metadata;

namespace AkkornStudio.SqlImport.IR;

public sealed record JoinExpr(
    string JoinId,
    SqlJoinType JoinType,
    SourceExpr RightSource,
    SqlExpression? OnExpr,
    int Ordinal,
    SqlResolutionStatus ResolutionStatus,
    SqlIrNodeMetadata NodeMetadata
);
