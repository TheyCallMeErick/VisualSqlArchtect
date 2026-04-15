using DBWeaver.Core;
using DBWeaver.Ddl.SchemaAnalysis.Application.Indexing;
using DBWeaver.Ddl.SchemaAnalysis.Application.Rules;
using DBWeaver.Ddl.SchemaAnalysis.Application.Validation;
using DBWeaver.Ddl.SchemaAnalysis.Domain.Contracts;
using DBWeaver.Ddl.SchemaAnalysis.Domain.Enums;
using DBWeaver.Metadata;

namespace DBWeaver.Tests.Unit.Ddl.SchemaAnalysis;

public sealed class MissingRequiredCommentRuleTests
{
    private readonly SchemaMetadataIndexBuilder _indexBuilder = new();
    private readonly MissingRequiredCommentRule _rule = new();

    [Fact]
    public async Task ExecuteAsync_EmitsIssue_ForTableWithoutComment()
    {
        DbMetadata metadata = CreateMetadata(tableComment: null);
        SchemaAnalysisProfile profile = CreateProfile(requiredTargets: ["Table"]);

        SchemaRuleExecutionResult result = await _rule.ExecuteAsync(CreateContext(metadata, profile));

        Assert.Contains(result.Issues, issue => issue.TargetType == SchemaTargetType.Table);
    }

    [Fact]
    public async Task ExecuteAsync_EmitsIssue_ForPrimaryKeyColumnWithoutComment()
    {
        DbMetadata metadata = CreateMetadata(primaryKeyComment: null);
        SchemaAnalysisProfile profile = CreateProfile(requiredTargets: ["PrimaryKeyColumn"]);

        SchemaRuleExecutionResult result = await _rule.ExecuteAsync(CreateContext(metadata, profile));

        Assert.Contains(result.Issues, issue => issue.ColumnName == "id");
    }

    [Fact]
    public async Task ExecuteAsync_EmitsIssue_ForAuditColumnWithoutComment()
    {
        DbMetadata metadata = CreateMetadata(auditColumnName: "created_at", auditComment: null);
        SchemaAnalysisProfile profile = CreateProfile(requiredTargets: ["AuditColumn"]);

        SchemaRuleExecutionResult result = await _rule.ExecuteAsync(CreateContext(metadata, profile));

        Assert.Contains(result.Issues, issue => issue.ColumnName == "created_at");
    }

    [Fact]
    public async Task ExecuteAsync_EmitsIssue_WhenCommentEqualsObjectName()
    {
        DbMetadata metadata = CreateMetadata(auditColumnName: "updated_at", auditComment: "updated_at");
        SchemaAnalysisProfile profile = CreateProfile(requiredTargets: ["AuditColumn"]);

        SchemaRuleExecutionResult result = await _rule.ExecuteAsync(CreateContext(metadata, profile));

        Assert.Contains(result.Issues, issue => issue.ColumnName == "updated_at");
    }

    [Fact]
    public async Task ExecuteAsync_DegradesSeverity_ForSqlite()
    {
        DbMetadata metadata = CreateMetadata(tableComment: null, provider: DatabaseProvider.SQLite);
        SchemaAnalysisProfile profile = CreateProfile(requiredTargets: ["Table"]);

        SchemaRuleExecutionResult result = await _rule.ExecuteAsync(CreateContext(metadata, profile));

        Assert.NotEmpty(result.Issues);
        Assert.All(result.Issues, issue => Assert.Equal(SchemaIssueSeverity.Info, issue.Severity));
        Assert.All(result.Issues, issue => Assert.Equal(0.6000, issue.Confidence));
    }

    private SchemaAnalysisExecutionContext CreateContext(
        DbMetadata metadata,
        SchemaAnalysisProfile profile
    )
    {
        SchemaMetadataIndexSnapshot indices = _indexBuilder.Build(metadata, profile);
        return new SchemaAnalysisExecutionContext(metadata, profile, indices, "fingerprint", "profile");
    }

    private static SchemaAnalysisProfile CreateProfile(IReadOnlyList<string> requiredTargets)
    {
        return SchemaAnalysisProfileNormalizer.CreateDefaultProfile() with
        {
            RequiredCommentTargets = requiredTargets,
        };
    }

    private static DbMetadata CreateMetadata(
        string? tableComment = "Orders comment",
        string? primaryKeyComment = "Identifier",
        string auditColumnName = "created_at",
        string? auditComment = "Audit",
        DatabaseProvider provider = DatabaseProvider.Postgres
    )
    {
        string schema = provider == DatabaseProvider.MySql ? string.Empty : "public";
        ForeignKeyRelation foreignKey = new(
            ConstraintName: "fk_orders_customer",
            ChildSchema: schema,
            ChildTable: "orders",
            ChildColumn: "customer_id",
            ParentSchema: schema,
            ParentTable: "customers",
            ParentColumn: "id",
            OnDelete: ReferentialAction.NoAction,
            OnUpdate: ReferentialAction.NoAction
        );

        TableMetadata orders = new(
            Schema: schema,
            Name: "orders",
            Kind: TableKind.Table,
            EstimatedRowCount: 100,
            Columns:
            [
                new ColumnMetadata("id", "integer", "integer", false, true, false, false, true, 1, Comment: primaryKeyComment),
                new ColumnMetadata("customer_id", "integer", "integer", false, false, true, false, true, 2, Comment: "Customer"),
                new ColumnMetadata(auditColumnName, "timestamp", "timestamp", false, false, false, false, false, 3, Comment: auditComment),
            ],
            Indexes: [],
            OutboundForeignKeys: [foreignKey],
            InboundForeignKeys: [],
            Comment: tableComment
        );

        TableMetadata customers = new(
            Schema: schema,
            Name: "customers",
            Kind: TableKind.Table,
            EstimatedRowCount: 10,
            Columns:
            [
                new ColumnMetadata("id", "integer", "integer", false, true, false, false, true, 1, Comment: "Id"),
            ],
            Indexes: [],
            OutboundForeignKeys: [],
            InboundForeignKeys: [foreignKey],
            Comment: "Customers"
        );

        return new DbMetadata(
            DatabaseName: "db",
            Provider: provider,
            ServerVersion: "1",
            CapturedAt: DateTimeOffset.UtcNow,
            Schemas: [new SchemaMetadata(schema, [orders, customers])],
            AllForeignKeys: [foreignKey]
        );
    }
}
