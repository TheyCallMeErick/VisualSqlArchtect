using DBWeaver.Ddl.SchemaAnalysis.Domain.Enums;

namespace DBWeaver.Ddl.SchemaAnalysis.Domain.Contracts;

public sealed record SchemaEvidence(
    EvidenceKind Kind,
    string Key,
    string Value,
    double Weight,
    string? SourcePath = null
);
