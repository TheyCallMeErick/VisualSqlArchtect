using DBWeaver.Ddl.SchemaAnalysis.Domain.Enums;

namespace DBWeaver.Ddl.SchemaAnalysis.Domain.Contracts;

public sealed record SchemaRuleExecutionDiagnostic(
    string Code,
    string Message,
    SchemaRuleCode? RuleCode,
    RuleExecutionState State,
    bool IsFatal
);
