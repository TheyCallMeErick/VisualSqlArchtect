using AkkornStudio.Core;
using AkkornStudio.Ddl.SchemaAnalysis.Application.Processing;
using AkkornStudio.Ddl.SchemaAnalysis.Domain.Contracts;
using AkkornStudio.Ddl.SchemaAnalysis.Domain.Enums;

namespace AkkornStudio.Tests.Unit.Ddl.SchemaAnalysis;

public sealed class SchemaMissingFkSqlCandidateFactoryTests
{
    private readonly SchemaMissingFkSqlCandidateFactory _factory = new();

    [Fact]
    public void CreateCandidate_ReturnsPostgresCandidate()
    {
        SqlFixCandidate? candidate = _factory.CreateCandidate(
            CreateIssue(isAmbiguous: false),
            DatabaseProvider.Postgres,
            "suggestion-1"
        );

        Assert.NotNull(candidate);
        Assert.Equal(SqlCandidateSafety.PotentiallyDestructive, candidate!.Safety);
        Assert.Equal(CandidateVisibility.VisibleReadOnly, candidate.Visibility);
        Assert.Equal("ALTER TABLE \"public\".\"orders\" ADD CONSTRAINT \"fk_orders_customer_id__customers_id\" FOREIGN KEY (\"customer_id\") REFERENCES \"public\".\"customers\" (\"id\");", candidate.Sql);
        Assert.Equal(4, candidate.PreconditionsSql.Count);
    }

    [Fact]
    public void CreateCandidate_ReturnsSqlServerCandidate()
    {
        SqlFixCandidate? candidate = _factory.CreateCandidate(
            CreateIssue(isAmbiguous: false, schemaName: "dbo"),
            DatabaseProvider.SqlServer,
            "suggestion-1"
        );

        Assert.NotNull(candidate);
        Assert.Equal("ALTER TABLE [dbo].[orders] ADD CONSTRAINT [fk_orders_customer_id__customers_id] FOREIGN KEY ([customer_id]) REFERENCES [dbo].[customers] ([id]);", candidate!.Sql);
    }

    [Fact]
    public void CreateCandidate_ReturnsMySqlCandidate()
    {
        SqlFixCandidate? candidate = _factory.CreateCandidate(
            CreateIssue(isAmbiguous: false, schemaName: null),
            DatabaseProvider.MySql,
            "suggestion-1"
        );

        Assert.NotNull(candidate);
        Assert.Equal("ALTER TABLE `orders` ADD CONSTRAINT `fk_orders_customer_id__customers_id` FOREIGN KEY (`customer_id`) REFERENCES `customers` (`id`);", candidate!.Sql);
    }

    [Fact]
    public void CreateCandidate_ReturnsNull_WhenIssueIsAmbiguous()
    {
        SqlFixCandidate? candidate = _factory.CreateCandidate(
            CreateIssue(isAmbiguous: true),
            DatabaseProvider.Postgres,
            "suggestion-1"
        );

        Assert.Null(candidate);
    }

    [Fact]
    public void CreateCandidate_ReturnsNull_WhenPreconditionsCannotBeGenerated()
    {
        SqlFixCandidate? candidate = _factory.CreateCandidate(
            CreateIssue(isAmbiguous: false),
            DatabaseProvider.SQLite,
            "suggestion-1"
        );

        Assert.Null(candidate);
    }

    private static SchemaIssue CreateIssue(bool isAmbiguous, string? schemaName = "public")
    {
        List<SchemaEvidence> evidence =
        [
            SchemaEvidenceFactory.MetadataFact("targetTable", "customers", 0.9),
            SchemaEvidenceFactory.MetadataFact("targetColumn", "id", 0.9),
        ];

        if (!string.IsNullOrWhiteSpace(schemaName))
        {
            evidence.Add(SchemaEvidenceFactory.MetadataFact("targetSchema", schemaName, 0.9));
        }

        return SchemaAnalysisContractValidatorTestData.CreateIssue(
            issueId: "issue-fk",
            tableName: "orders",
            columnName: "customer_id",
            targetType: SchemaTargetType.Column,
            evidence: evidence,
            suggestions: []
        ) with
        {
            SchemaName = schemaName,
            RuleCode = SchemaRuleCode.MISSING_FK,
            IsAmbiguous = isAmbiguous,
        };
    }
}
