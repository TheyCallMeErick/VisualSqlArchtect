namespace AkkornStudio.Ddl.SchemaAnalysis.Domain.Contracts;

public sealed record SchemaAnalysisPartialState(
    bool IsPartial,
    string ReasonCode,
    int CompletedRules,
    int TotalRules
);
