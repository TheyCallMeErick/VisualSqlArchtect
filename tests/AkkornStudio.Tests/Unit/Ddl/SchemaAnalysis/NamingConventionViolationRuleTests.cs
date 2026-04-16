using AkkornStudio.Core;
using AkkornStudio.Ddl.SchemaAnalysis.Application.Indexing;
using AkkornStudio.Ddl.SchemaAnalysis.Application.Rules;
using AkkornStudio.Ddl.SchemaAnalysis.Application.Validation;
using AkkornStudio.Ddl.SchemaAnalysis.Domain.Contracts;
using AkkornStudio.Ddl.SchemaAnalysis.Domain.Enums;
using AkkornStudio.Metadata;

namespace AkkornStudio.Tests.Unit.Ddl.SchemaAnalysis;

public sealed class NamingConventionViolationRuleTests
{
    private readonly SchemaMetadataIndexBuilder _indexBuilder = new();
    private readonly NamingConventionViolationRule _rule = new();

    [Fact]
    public async Task ExecuteAsync_EmitsIssue_ForInvalidTableName()
    {
        DbMetadata metadata = CreateMetadata(
            tableName: "CustomerOrders",
            columnName: "customer_id",
            constraintName: "fk_orders_customer"
        );

        SchemaRuleExecutionResult result = await _rule.ExecuteAsync(CreateContext(metadata));

        Assert.Contains(result.Issues, issue => issue.TargetType == SchemaTargetType.Table);
    }

    [Fact]
    public async Task ExecuteAsync_EmitsIssue_ForInvalidColumnName()
    {
        DbMetadata metadata = CreateMetadata(
            tableName: "orders",
            columnName: "CustomerID",
            constraintName: "fk_orders_customer"
        );

        SchemaRuleExecutionResult result = await _rule.ExecuteAsync(CreateContext(metadata));

        Assert.Contains(result.Issues, issue => issue.TargetType == SchemaTargetType.Column);
    }

    [Fact]
    public async Task ExecuteAsync_EmitsIssue_ForInvalidConstraintName()
    {
        DbMetadata metadata = CreateMetadata(
            tableName: "orders",
            columnName: "customer_id",
            constraintName: "FKOrdersCustomer"
        );

        SchemaRuleExecutionResult result = await _rule.ExecuteAsync(CreateContext(metadata));

        Assert.Contains(result.Issues, issue => issue.TargetType == SchemaTargetType.Constraint);
    }

    [Fact]
    public async Task ExecuteAsync_DoesNotEmit_WhenConventionIsMixedAllowed()
    {
        DbMetadata metadata = CreateMetadata(
            tableName: "CustomerOrders",
            columnName: "CustomerID",
            constraintName: "FKOrdersCustomer"
        );
        SchemaAnalysisProfile profile = SchemaAnalysisProfileNormalizer.CreateDefaultProfile() with
        {
            NamingConvention = NamingConvention.MixedAllowed,
        };

        SchemaRuleExecutionResult result = await _rule.ExecuteAsync(CreateContext(metadata, profile));

        Assert.Empty(result.Issues);
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
        string constraintName
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
