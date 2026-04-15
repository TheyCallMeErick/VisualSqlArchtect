namespace AkkornStudio.SqlImport.IR.Metadata;

public sealed record IrMetrics(
    int TotalSelectItems,
    int TotalSources,
    int TotalJoins,
    int TotalExpressions,
    int UnresolvedExpressions,
    int AmbiguousExpressions,
    int PartialExpressions
);
