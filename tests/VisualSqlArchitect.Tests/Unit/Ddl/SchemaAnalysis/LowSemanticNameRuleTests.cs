using DBWeaver.Core;
using DBWeaver.Ddl.SchemaAnalysis.Application.Indexing;
using DBWeaver.Ddl.SchemaAnalysis.Application.Rules;
using DBWeaver.Ddl.SchemaAnalysis.Application.Validation;
using DBWeaver.Ddl.SchemaAnalysis.Domain.Contracts;
using DBWeaver.Ddl.SchemaAnalysis.Domain.Enums;
using DBWeaver.Metadata;

namespace DBWeaver.Tests.Unit.Ddl.SchemaAnalysis;

public sealed class LowSemanticNameRuleTests
{
    private readonly SchemaMetadataIndexBuilder _indexBuilder = new();
    private readonly LowSemanticNameRule _rule = new();

    [Fact]
    public async Task ExecuteAsync_EmitsIssue_WhenPrincipalTokenIsInDenylist()
    {
        DbMetadata metadata = CreateMetadata(tableName: "orders", columnName: "valor");

        SchemaRuleExecutionResult result = await _rule.ExecuteAsync(CreateContext(metadata));

        Assert.Contains(result.Issues, issue => issue.TargetType == SchemaTargetType.Column);
    }

    [Fact]
    public async Task ExecuteAsync_EmitsIssue_WhenWeakRegexMatches()
    {
        DbMetadata metadata = CreateMetadata(tableName: "tmp1", columnName: "customer_id");

        SchemaRuleExecutionResult result = await _rule.ExecuteAsync(CreateContext(metadata));

        Assert.Contains(result.Issues, issue => issue.TargetType == SchemaTargetType.Table);
    }

    [Fact]
    public async Task ExecuteAsync_DoesNotEmit_WhenNameIsAllowlisted()
    {
        DbMetadata metadata = CreateMetadata(tableName: "orders", columnName: "valor");
        SchemaAnalysisProfile profile = SchemaAnalysisProfileNormalizer.CreateDefaultProfile() with
        {
            NameAllowlist = ["valor"],
        };

        SchemaRuleExecutionResult result = await _rule.ExecuteAsync(CreateContext(metadata, profile));

        Assert.Empty(result.Issues);
    }

    [Fact]
    public async Task ExecuteAsync_DoesNotEmit_WhenNameContainsOnlyStructuralTokens()
    {
        DbMetadata metadata = CreateMetadata(tableName: "orders", columnName: "id_fk_ref_code");

        SchemaRuleExecutionResult result = await _rule.ExecuteAsync(CreateContext(metadata));

        Assert.Empty(result.Issues);
    }

    [Fact]
    public async Task ExecuteAsync_DoesNotEvaluateConstraints()
    {
        DbMetadata metadata = CreateMetadata(tableName: "orders", columnName: "customer_id", constraintName: "tmp_constraint");

        SchemaRuleExecutionResult result = await _rule.ExecuteAsync(CreateContext(metadata));

        Assert.DoesNotContain(result.Issues, issue => issue.TargetType == SchemaTargetType.Constraint);
    }

    private SchemaAnalysisExecutionContext CreateContext(
        DbMetadata metadata,
        SchemaAnalysisProfile? profile = null
    )
    {
        SchemaAnalysisProfile effectiveProfile = profile ?? SchemaAnalysisProfileNormalizer.CreateDefaultProfile();
        SchemaMetadataIndexSnapshot indices = _indexBuilder.Build(metadata, effectiveProfile);
        return new SchemaAnalysisExecutionContext(metadata, effectiveProfile, indices, "fingerprint", "profile");
    }

    private static DbMetadata CreateMetadata(
        string tableName,
        string columnName,
        string constraintName = "fk_orders_customer"
    )
    {
        ForeignKeyRelation foreignKey = new(
            ConstraintName: constraintName,
            ChildSchema: "public",
            ChildTable: tableName,
            ChildColumn: columnName,
            ParentSchema: "public",
            ParentTable: "customers",
            ParentColumn: "id",
            OnDelete: ReferentialAction.NoAction,
            OnUpdate: ReferentialAction.NoAction
        );

        TableMetadata table = new(
            Schema: "public",
            Name: tableName,
            Kind: TableKind.Table,
            EstimatedRowCount: 10,
            Columns:
            [
                new ColumnMetadata(columnName, "integer", "integer", false, false, false, false, true, 1, Comment: "Column"),
            ],
            Indexes: [],
            OutboundForeignKeys: [foreignKey],
            InboundForeignKeys: [],
            Comment: "Orders"
        );

        TableMetadata parent = new(
            Schema: "public",
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
            Provider: DatabaseProvider.Postgres,
            ServerVersion: "1",
            CapturedAt: DateTimeOffset.UtcNow,
            Schemas: [new SchemaMetadata("public", [table, parent])],
            AllForeignKeys: [foreignKey]
        );
    }
}
