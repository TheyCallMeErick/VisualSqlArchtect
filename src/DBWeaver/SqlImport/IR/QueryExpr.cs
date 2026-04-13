using DBWeaver.SqlImport.IR.Expressions;
using DBWeaver.SqlImport.IR.Sources;

namespace DBWeaver.SqlImport.IR;

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
