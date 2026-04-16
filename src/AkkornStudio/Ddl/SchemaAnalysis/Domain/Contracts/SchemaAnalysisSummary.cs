using AkkornStudio.Ddl.SchemaAnalysis.Domain.Enums;

namespace AkkornStudio.Ddl.SchemaAnalysis.Domain.Contracts;

public sealed record SchemaAnalysisSummary(
    int TotalIssues,
    int InfoCount,
    int WarningCount,
    int CriticalCount,
    IReadOnlyDictionary<SchemaRuleCode, int> PerRuleCount,
    IReadOnlyDictionary<string, int> PerTableCount
);
