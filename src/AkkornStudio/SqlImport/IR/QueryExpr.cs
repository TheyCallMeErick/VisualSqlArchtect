using AkkornStudio.SqlImport.IR.Expressions;
using AkkornStudio.SqlImport.IR.Sources;

namespace AkkornStudio.SqlImport.IR;

public sealed record QueryExpr(
    IReadOnlyList<SelectItemExpr> SelectItems,
    SourceExpr FromSource,
    IReadOnlyList<JoinExpr> Joins,
    SqlExpression? WhereExpr,
    IReadOnlyList<SqlExpression> GroupBy,
    SqlExpression? HavingExpr,
    IReadOnlyList<OrderByExpr> OrderBy,
    LimitOrTopExpr? LimitOrTop,
    IReadOnlyList<SetOperationExpr> SetOperations
);
