using AkkornStudio.Ddl.SchemaAnalysis.Domain.Contracts;

namespace AkkornStudio.Ddl.SchemaAnalysis.Application.Validation;

public sealed record SchemaMetadataValidationResult(
    bool IsValid,
    IReadOnlyList<SchemaRuleExecutionDiagnostic> Diagnostics
);
