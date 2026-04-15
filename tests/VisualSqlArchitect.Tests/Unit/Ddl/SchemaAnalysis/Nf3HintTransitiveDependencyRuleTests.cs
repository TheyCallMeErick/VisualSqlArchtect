using DBWeaver.Core;
using DBWeaver.Ddl.SchemaAnalysis.Application.Indexing;
using DBWeaver.Ddl.SchemaAnalysis.Application.Rules;
using DBWeaver.Ddl.SchemaAnalysis.Application.Validation;
using DBWeaver.Ddl.SchemaAnalysis.Domain.Contracts;
using DBWeaver.Metadata;

namespace DBWeaver.Tests.Unit.Ddl.SchemaAnalysis;

public sealed class Nf3HintTransitiveDependencyRuleTests
{
    private readonly SchemaMetadataIndexBuilder _indexBuilder = new();
    private readonly Nf3HintTransitiveDependencyRule _rule = new();

    [Fact]
    public async Task ExecuteAsync_Emits_WhenCustomerIdAndCustomerNameExist()
    {
        DbMetadata metadata = CreateMetadata(
            [
                new ColumnMetadata("id", "integer", "integer", false, true, false, false, true, 1, Comment: "Id"),
                new ColumnMetadata("customer_id", "integer", "integer", false, false, false, false, true, 2, Comment: "Customer"),
                new ColumnMetadata("customer_name", "text", "text", false, false, false, false, false, 3, Comment: "Customer name"),
            ]
        );

        SchemaRuleExecutionResult result = await _rule.ExecuteAsync(CreateContext(metadata));

        SchemaIssue issue = Assert.Single(result.Issues);
        Assert.Equal("customer_name", issue.ColumnName);
        Assert.Equal(0.7000, issue.Confidence);
    }

    [Fact]
    public async Task ExecuteAsync_DoesNotEmit_WhenStatusIsIsolated()
    {
        DbMetadata metadata = CreateMetadata(
            [
                new ColumnMetadata("id", "integer", "integer", false, true, false, false, true, 1, Comment: "Id"),
                new ColumnMetadata("status", "text", "text", false, false, false, false, false, 2, Comment: "Status"),
            ]
        );

        SchemaRuleExecutionResult result = await _rule.ExecuteAsync(CreateContext(metadata));

        Assert.Empty(result.Issues);
    }

    [Fact]
    public async Task ExecuteAsync_AllowlistSuppressesEmission_WhenApplicable()
    {
        DbMetadata metadata = CreateMetadata(
            [
                new ColumnMetadata("id", "integer", "integer", false, true, false, false, true, 1, Comment: "Id"),
                new ColumnMetadata("customer_id", "integer", "integer", false, false, false, false, true, 2, Comment: "Customer"),
                new ColumnMetadata("customer_name", "text", "text", false, false, false, false, false, 3, Comment: "Customer name"),
            ]
        );
        SchemaAnalysisProfile profile = SchemaAnalysisProfileNormalizer.CreateDefaultProfile() with
        {
            SemiStructuredPayloadAllowlist = ["public.orders.customer_name"],
            RuleSettings = SchemaAnalysisProfileNormalizer.CreateDefaultProfile().RuleSettings,
        };

        SchemaRuleExecutionResult result = await _rule.ExecuteAsync(CreateContext(metadata, profile));

        Assert.Empty(result.Issues);
    }

    [Fact]
    public async Task ExecuteAsync_BoostsConfidence_WhenAdditionalStatusColumnExists()
    {
        DbMetadata metadata = CreateMetadata(
            [
                new ColumnMetadata("id", "integer", "integer", false, true, false, false, true, 1, Comment: "Id"),
                new ColumnMetadata("customer_id", "integer", "integer", false, false, false, false, true, 2, Comment: "Customer"),
                new ColumnMetadata("customer_name", "text", "text", false, false, false, false, false, 3, Comment: "Customer name"),
                new ColumnMetadata("customer_status", "text", "text", false, false, false, false, false, 4, Comment: "Customer status"),
            ]
        );

        SchemaRuleExecutionResult result = await _rule.ExecuteAsync(CreateContext(metadata));

        SchemaIssue issue = Assert.Single(result.Issues);
        Assert.Equal(0.8500, issue.Confidence);
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

    private static DbMetadata CreateMetadata(IReadOnlyList<ColumnMetadata> columns)
    {
        TableMetadata orders = new(
            Schema: "public",
            Name: "orders",
            Kind: TableKind.Table,
            EstimatedRowCount: 100,
            Columns: columns,
            Indexes: [new IndexMetadata("pk_orders", true, true, true, ["id"])],
            OutboundForeignKeys: [],
            InboundForeignKeys: [],
            Comment: "Orders"
        );

        return new DbMetadata(
            DatabaseName: "db",
            Provider: DatabaseProvider.Postgres,
            ServerVersion: "16",
            CapturedAt: DateTimeOffset.UtcNow,
            Schemas: [new SchemaMetadata("public", [orders])],
            AllForeignKeys: []
        );
    }
}
