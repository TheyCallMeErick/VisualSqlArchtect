using DBWeaver.Core;
using DBWeaver.Ddl.SchemaAnalysis.Application.Processing;
using DBWeaver.Ddl.SchemaAnalysis.Domain.Enums;

namespace DBWeaver.Tests.Unit.Ddl.SchemaAnalysis;

public sealed class SchemaDeterministicIdFactoryTests
{
    [Fact]
    public void CreateIssueId_IsRepeatable_ForSameLogicalPayload()
    {
        string first = SchemaDeterministicIdFactory.CreateIssueId(
            SchemaRuleCode.MISSING_FK,
            SchemaTargetType.Column,
            " public ",
            " orders ",
            " customer_id ",
            null,
            "Missing FK",
            "Issue message",
            0.8500,
            false
        );
        string second = SchemaDeterministicIdFactory.CreateIssueId(
            SchemaRuleCode.MISSING_FK,
            SchemaTargetType.Column,
            "public",
            "orders",
            "customer_id",
            null,
            "Missing FK",
            "Issue message",
            0.8500,
            false
        );

        Assert.Equal(first, second);
    }

    [Fact]
    public void CreateIssueId_Changes_WhenMessageChanges()
    {
        string first = SchemaDeterministicIdFactory.CreateIssueId(
            SchemaRuleCode.MISSING_FK,
            SchemaTargetType.Column,
            "public",
            "orders",
            "customer_id",
            null,
            "Missing FK",
            "Message A",
            0.8500,
            false
        );
        string second = SchemaDeterministicIdFactory.CreateIssueId(
            SchemaRuleCode.MISSING_FK,
            SchemaTargetType.Column,
            "public",
            "orders",
            "customer_id",
            null,
            "Missing FK",
            "Message B",
            0.8500,
            false
        );

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void CreateSuggestionId_Changes_WhenConfidenceChanges()
    {
        string first = SchemaDeterministicIdFactory.CreateSuggestionId("issue-1", "Title", "Description", 0.9000);
        string second = SchemaDeterministicIdFactory.CreateSuggestionId("issue-1", "Title", "Description", 0.8000);

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void CreateCandidateId_IsRepeatable_ForSamePayload()
    {
        string first = SchemaDeterministicIdFactory.CreateCandidateId(
            "suggestion-1",
            DatabaseProvider.Postgres,
            "Add FK",
            "ALTER TABLE ...",
            SqlCandidateSafety.PotentiallyDestructive
        );
        string second = SchemaDeterministicIdFactory.CreateCandidateId(
            "suggestion-1",
            DatabaseProvider.Postgres,
            "Add FK",
            "ALTER TABLE ...",
            SqlCandidateSafety.PotentiallyDestructive
        );

        Assert.Equal(first, second);
    }
}
