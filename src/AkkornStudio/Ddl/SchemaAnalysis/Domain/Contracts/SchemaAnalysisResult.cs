using AkkornStudio.Core;
using AkkornStudio.Ddl.SchemaAnalysis.Domain.Enums;

namespace AkkornStudio.Ddl.SchemaAnalysis.Domain.Contracts;

public sealed record SchemaAnalysisResult(
    string AnalysisId,
    SchemaAnalysisStatus Status,
    DatabaseProvider Provider,
    string DatabaseName,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset CompletedAtUtc,
    long DurationMs,
    string MetadataFingerprint,
    string ProfileContentHash,
    int ProfileVersion,
    SchemaAnalysisPartialState PartialState,
    IReadOnlyList<SchemaIssue> Issues,
    IReadOnlyList<SchemaRuleExecutionDiagnostic> Diagnostics,
    SchemaAnalysisSummary Summary
);
