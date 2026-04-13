using DBWeaver.Ddl.SchemaAnalysis.Domain.Contracts;

namespace DBWeaver.Ddl.SchemaAnalysis.Application.Validation;

public sealed record SchemaMetadataValidationResult(
    bool IsValid,
    IReadOnlyList<SchemaRuleExecutionDiagnostic> Diagnostics
);
