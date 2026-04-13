using DBWeaver.Ddl.SchemaAnalysis.Application.Processing;
using DBWeaver.Ddl.SchemaAnalysis.Domain.Contracts;
using DBWeaver.Ddl.SchemaAnalysis.Domain.Enums;

namespace DBWeaver.Tests.Unit.Ddl.SchemaAnalysis;

public sealed class SchemaIssueOrdererTests
{
    [Fact]
    public void Order_SortsIssues_ByNormativeFinalOrdering()
    {
        SchemaIssueOrderer orderer = new();
        SchemaIssue info = CreateIssue("c", SchemaIssueSeverity.Info, 0.99, "z_orders", "z_column", null);
        SchemaIssue warningHigh = CreateIssue("b", SchemaIssueSeverity.Warning, 0.90, "a_orders", "b_column", null);
        SchemaIssue warningLow = CreateIssue("a", SchemaIssueSeverity.Warning, 0.80, "a_orders", "a_column", null);
        SchemaIssue critical = CreateIssue("d", SchemaIssueSeverity.Critical, 0.70, "m_orders", "m_column", null);

        IReadOnlyList<SchemaIssue> ordered = orderer.Order([info, warningLow, critical, warningHigh]);

        Assert.Equal(["d", "b", "a", "c"], ordered.Select(static issue => issue.IssueId));
    }

    private static SchemaIssue CreateIssue(
        string issueId,
        SchemaIssueSeverity severity,
        double confidence,
        string tableName,
        string? columnName,
        string? constraintName
    ) =>
        new(
            IssueId: issueId,
            RuleCode: SchemaRuleCode.MISSING_FK,
            Severity: severity,
            Confidence: confidence,
            TargetType: SchemaTargetType.Column,
            SchemaName: "public",
            TableName: tableName,
            ColumnName: columnName,
            ConstraintName: constraintName,
            Title: "Issue",
            Message: issueId,
            Evidence: [new SchemaEvidence(EvidenceKind.MetadataFact, "k", "v", 1.0)],
            Suggestions: [],
            IsAmbiguous: false
        );
}
