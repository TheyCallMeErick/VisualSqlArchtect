using DBWeaver.SqlImport.Contracts;
using DBWeaver.SqlImport.IR.Expressions;
using DBWeaver.SqlImport.IR.Sources;
using DBWeaver.SqlImport.IR.Metadata;

namespace DBWeaver.SqlImport.IR;

public sealed record JoinExpr(
    string JoinId,
    SqlJoinType JoinType,
    SourceExpr RightSource,
    SqlExpression? OnExpr,
    int Ordinal,
    SqlResolutionStatus ResolutionStatus,
    SqlIrNodeMetadata NodeMetadata
);
