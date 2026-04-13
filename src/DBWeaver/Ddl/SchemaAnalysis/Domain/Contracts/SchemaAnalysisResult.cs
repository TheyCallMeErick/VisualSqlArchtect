using DBWeaver.Core;
using DBWeaver.Ddl.SchemaAnalysis.Domain.Enums;

namespace DBWeaver.Ddl.SchemaAnalysis.Domain.Contracts;

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
