using AkkornStudio.Ddl.SchemaAnalysis.Domain.Enums;

namespace AkkornStudio.Ddl.SchemaAnalysis.Domain.Contracts;

public sealed record SchemaEvidence(
    EvidenceKind Kind,
    string Key,
    string Value,
    double Weight,
    string? SourcePath = null
);
