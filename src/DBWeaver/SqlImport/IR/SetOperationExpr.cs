namespace DBWeaver.SqlImport.IR;

public sealed record SetOperationExpr(string Kind, QueryExpr RightQuery, bool IsAll);
