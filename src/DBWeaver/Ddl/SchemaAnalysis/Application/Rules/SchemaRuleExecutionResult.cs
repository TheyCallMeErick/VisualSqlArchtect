using DBWeaver.Ddl.SchemaAnalysis.Domain.Contracts;

namespace DBWeaver.Ddl.SchemaAnalysis.Application.Rules;

public sealed record SchemaRuleExecutionResult(
    IReadOnlyList<SchemaIssue> Issues,
    IReadOnlyList<SchemaRuleExecutionDiagnostic> Diagnostics
);
