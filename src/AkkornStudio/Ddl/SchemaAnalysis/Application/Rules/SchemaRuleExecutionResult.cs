using AkkornStudio.Ddl.SchemaAnalysis.Domain.Contracts;

namespace AkkornStudio.Ddl.SchemaAnalysis.Application.Rules;

public sealed record SchemaRuleExecutionResult(
    IReadOnlyList<SchemaIssue> Issues,
    IReadOnlyList<SchemaRuleExecutionDiagnostic> Diagnostics
);
