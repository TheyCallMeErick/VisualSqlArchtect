namespace AkkornStudio.SqlImport.IR.Expressions;

public sealed record CaseWhenBranchExpr(SqlExpression WhenExpression, SqlExpression ThenExpression);
