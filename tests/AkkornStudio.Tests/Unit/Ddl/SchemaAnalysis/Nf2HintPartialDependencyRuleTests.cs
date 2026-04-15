using AkkornStudio.Core;
using AkkornStudio.Ddl.SchemaAnalysis.Application.Indexing;
using AkkornStudio.Ddl.SchemaAnalysis.Application.Rules;
using AkkornStudio.Ddl.SchemaAnalysis.Application.Validation;
using AkkornStudio.Ddl.SchemaAnalysis.Domain.Contracts;
using AkkornStudio.Ddl.SchemaAnalysis.Domain.Enums;
using AkkornStudio.Metadata;

namespace AkkornStudio.Tests.Unit.Ddl.SchemaAnalysis;

public sealed class Nf2HintPartialDependencyRuleTests
{
    private readonly SchemaMetadataIndexBuilder _indexBuilder = new();
    private readonly Nf2HintPartialDependencyRule _rule = new();

    [Fact]
    public async Task ExecuteAsync_Emits_WhenCompositePkHasDescriptiveColumnLinkedToSingleComponent()
    {
        DbMetadata metadata = CreateMetadata(
            includeCompositePrimaryKey: true,
            candidateColumn: new ColumnMetadata("product_name", "text", "text", false, false, false, false, false, 3, Comment: "Product name")
        );

        SchemaRuleExecutionResult result = await _rule.ExecuteAsync(CreateContext(metadata));

        SchemaIssue issue = Assert.Single(result.Issues);
        Assert.Equal(SchemaRuleCode.NF2_HINT_PARTIAL_DEPENDENCY, issue.RuleCode);
        Assert.Equal("product_name", issue.ColumnName);
        Assert.Equal(0.8500, issue.Confidence);
    }

    [Fact]
    public async Task ExecuteAsync_DoesNotEmit_WhenTableDoesNotHaveCompositePrimaryKey()
    {
        DbMetadata metadata = CreateMetadata(
            includeCompositePrimaryKey: false,
            candidateColumn: new ColumnMetadata("product_name", "text", "text", false, false, false, false, false, 2, Comment: "Product name")
        );

        SchemaRuleExecutionResult result = await _rule.ExecuteAsync(CreateContext(metadata));

        Assert.Empty(result.Issues);
    }

    [Fact]
    public async Task ExecuteAsync_DoesNotEmit_WhenUniqueIndexWithFullPkMitigatesScore()
    {
        DbMetadata metadata = CreateMetadata(
            includeCompositePrimaryKey: true,
            candidateColumn: new ColumnMetadata("product_name", "text", "text", false, false, false, false, false, 3, Comment: "Product name"),
            includeMitigatingUniqueIndex: true
        );

        SchemaRuleExecutionResult result = await _rule.ExecuteAsync(CreateContext(metadata));

        Assert.Empty(result.Issues);
    }

    [Fact]
    public async Task ExecuteAsync_DoesNotEmit_WhenOnlyStrongAssociationExists()
    {
        DbMetadata metadata = CreateMetadata(
            includeCompositePrimaryKey: true,
            candidateColumn: new ColumnMetadata("product_amount", "numeric", "numeric(10,2)", false, false, false, false, false, 3, Comment: "Product amount")
        );

        SchemaRuleExecutionResult result = await _rule.ExecuteAsync(CreateContext(metadata));

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
        bool includeCompositePrimaryKey,
        ColumnMetadata candidateColumn,
        bool includeMitigatingUniqueIndex = false
    )
    {
        ForeignKeyRelation orderForeignKey = new(
            ConstraintName: "fk_order_items_order",
            ChildSchema: "public",
            ChildTable: "order_items",
            ChildColumn: "order_id",
            ParentSchema: "public",
            ParentTable: "orders",
            ParentColumn: "id",
            OnDelete: ReferentialAction.NoAction,
            OnUpdate: ReferentialAction.NoAction
        );

        ForeignKeyRelation productForeignKey = new(
            ConstraintName: "fk_order_items_product",
            ChildSchema: "public",
            ChildTable: "order_items",
            ChildColumn: "product_id",
            ParentSchema: "public",
            ParentTable: "products",
            ParentColumn: "id",
            OnDelete: ReferentialAction.NoAction,
            OnUpdate: ReferentialAction.NoAction
        );

        List<ColumnMetadata> orderItemColumns =
        [
            new ColumnMetadata("order_id", "integer", "integer", false, true, true, false, true, 1, Comment: "Order"),
            new ColumnMetadata(
                "product_id",
                "integer",
                "integer",
                false,
                includeCompositePrimaryKey,
                true,
                false,
                true,
                includeCompositePrimaryKey ? 2 : 3,
                Comment: "Product"
            ),
            candidateColumn,
        ];

        List<IndexMetadata> indexes =
        [
            new IndexMetadata(
                "pk_order_items",
                true,
                true,
                true,
                includeCompositePrimaryKey ? ["order_id", "product_id"] : ["order_id"]
            ),
        ];

        if (includeMitigatingUniqueIndex)
        {
            indexes.Add(
                new IndexMetadata(
                    "uq_order_items_order_product_name",
                    true,
                    false,
                    false,
                    ["order_id", "product_id", candidateColumn.Name]
                )
            );
        }

        TableMetadata orderItems = new(
            Schema: "public",
            Name: "order_items",
            Kind: TableKind.Table,
            EstimatedRowCount: 1_000,
            Columns: orderItemColumns,
            Indexes: indexes,
            OutboundForeignKeys: includeCompositePrimaryKey ? [orderForeignKey, productForeignKey] : [orderForeignKey],
            InboundForeignKeys: [],
            Comment: "Order items"
        );

        TableMetadata orders = new(
            Schema: "public",
            Name: "orders",
            Kind: TableKind.Table,
            EstimatedRowCount: 200,
            Columns:
            [
                new ColumnMetadata("id", "integer", "integer", false, true, false, false, true, 1, Comment: "Order id"),
            ],
            Indexes: [new IndexMetadata("pk_orders", true, true, true, ["id"])],
            OutboundForeignKeys: [],
            InboundForeignKeys: [orderForeignKey],
            Comment: "Orders"
        );

        TableMetadata products = new(
            Schema: "public",
            Name: "products",
            Kind: TableKind.Table,
            EstimatedRowCount: 50,
            Columns:
            [
                new ColumnMetadata("id", "integer", "integer", false, true, false, false, true, 1, Comment: "Product id"),
                new ColumnMetadata("name", "text", "text", false, false, false, false, false, 2, Comment: "Name"),
            ],
            Indexes: [new IndexMetadata("pk_products", true, true, true, ["id"])],
            OutboundForeignKeys: [],
            InboundForeignKeys: includeCompositePrimaryKey ? [productForeignKey] : [],
            Comment: "Products"
        );

        return new DbMetadata(
            DatabaseName: "db",
            Provider: DatabaseProvider.Postgres,
            ServerVersion: "16",
            CapturedAt: DateTimeOffset.UtcNow,
            Schemas: [new SchemaMetadata("public", [orderItems, orders, products])],
            AllForeignKeys: includeCompositePrimaryKey ? [orderForeignKey, productForeignKey] : [orderForeignKey]
        );
    }
}
