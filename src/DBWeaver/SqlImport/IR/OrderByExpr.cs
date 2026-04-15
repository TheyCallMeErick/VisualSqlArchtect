using DBWeaver.SqlImport.Contracts;
using DBWeaver.SqlImport.IR.Expressions;
using DBWeaver.SqlImport.IR.Metadata;

namespace DBWeaver.SqlImport.IR;

public sealed record OrderByExpr(
    SqlExpression Expression,
    bool Descending,
    SqlResolutionStatus ResolutionStatus,
    SqlIrNodeMetadata NodeMetadata
);
