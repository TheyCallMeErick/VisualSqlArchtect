using DBWeaver.Ddl.SchemaAnalysis.Application.Processing;
using DBWeaver.Ddl.SchemaAnalysis.Domain.Contracts;
using DBWeaver.Ddl.SchemaAnalysis.Domain.Enums;

namespace DBWeaver.Tests.Unit.Ddl.SchemaAnalysis;

public sealed class SchemaIssueDeduplicatorTests
{
    [Fact]
    public void Deduplicate_KeepsIssueWithHighestConfidence_WhenEquivalent()
    {
        SchemaIssueDeduplicator deduplicator = new();
        SchemaIssue low = CreateIssue("a", SchemaIssueSeverity.Warning, 0.70, "Same   message");
        SchemaIssue high = CreateIssue("b", SchemaIssueSeverity.Warning, 0.90, "same message");

        IReadOnlyList<SchemaIssue> deduped = deduplicator.Deduplicate([low, high]);

        Assert.Single(deduped);
        Assert.Equal("b", deduped[0].IssueId);
    }

    [Fact]
    public void Deduplicate_KeepsIssueWithHighestSeverity_WhenConfidenceTies()
    {
        SchemaIssueDeduplicator deduplicator = new();
        SchemaIssue warning = CreateIssue("a", SchemaIssueSeverity.Warning, 0.90, "same message");
        SchemaIssue critical = CreateIssue("b", SchemaIssueSeverity.Critical, 0.90, "same message");

        IReadOnlyList<SchemaIssue> deduped = deduplicator.Deduplicate([warning, critical]);

        Assert.Single(deduped);
        Assert.Equal("b", deduped[0].IssueId);
    }

    private static SchemaIssue CreateIssue(
        string issueId,
        SchemaIssueSeverity severity,
        double confidence,
        string message
    ) =>
        new(
            IssueId: issueId,
            RuleCode: SchemaRuleCode.MISSING_FK,
            Severity: severity,
            Confidence: confidence,
            TargetType: SchemaTargetType.Column,
            SchemaName: "public",
            TableName: "orders",
            ColumnName: "customer_id",
            ConstraintName: null,
            Title: "Issue",
            Message: message,
            Evidence: [new SchemaEvidence(EvidenceKind.MetadataFact, "k", "v", 1.0)],
            Suggestions: [],
            IsAmbiguous: false
        );
}
