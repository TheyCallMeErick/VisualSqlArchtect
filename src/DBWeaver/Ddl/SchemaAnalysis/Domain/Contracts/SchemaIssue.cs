using DBWeaver.Ddl.SchemaAnalysis.Domain.Enums;

namespace DBWeaver.Ddl.SchemaAnalysis.Domain.Contracts;

public sealed record SchemaIssue(
    string IssueId,
    SchemaRuleCode RuleCode,
    SchemaIssueSeverity Severity,
    double Confidence,
    SchemaTargetType TargetType,
    string? SchemaName,
    string? TableName,
    string? ColumnName,
    string? ConstraintName,
    string Title,
    string Message,
    IReadOnlyList<SchemaEvidence> Evidence,
    IReadOnlyList<SchemaSuggestion> Suggestions,
    bool IsAmbiguous
);
