using AkkornStudio.SqlImport.Contracts;
using AkkornStudio.SqlImport.IR.Expressions;
using AkkornStudio.SqlImport.IR.Metadata;

namespace AkkornStudio.SqlImport.IR;

public sealed record OrderByExpr(
    SqlExpression Expression,
    bool Descending,
    SqlResolutionStatus ResolutionStatus,
    SqlIrNodeMetadata NodeMetadata
);
