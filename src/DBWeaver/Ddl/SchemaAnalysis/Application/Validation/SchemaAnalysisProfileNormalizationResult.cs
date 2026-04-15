using DBWeaver.Ddl.SchemaAnalysis.Domain.Contracts;

namespace DBWeaver.Ddl.SchemaAnalysis.Application.Validation;

public sealed record SchemaAnalysisProfileNormalizationResult(
    SchemaAnalysisProfile Profile,
    IReadOnlyList<SchemaRuleExecutionDiagnostic> Diagnostics
);
