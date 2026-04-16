using AkkornStudio.Ddl.SchemaAnalysis.Domain.Enums;

namespace AkkornStudio.Ddl.SchemaAnalysis.Domain.Contracts;

public sealed record SchemaRuleExecutionDiagnostic(
    string Code,
    string Message,
    SchemaRuleCode? RuleCode,
    RuleExecutionState State,
    bool IsFatal
);
