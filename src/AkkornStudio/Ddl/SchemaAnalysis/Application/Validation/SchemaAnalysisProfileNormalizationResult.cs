using AkkornStudio.Ddl.SchemaAnalysis.Domain.Contracts;

namespace AkkornStudio.Ddl.SchemaAnalysis.Application.Validation;

public sealed record SchemaAnalysisProfileNormalizationResult(
    SchemaAnalysisProfile Profile,
    IReadOnlyList<SchemaRuleExecutionDiagnostic> Diagnostics
);
