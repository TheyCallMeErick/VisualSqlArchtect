using AkkornStudio.Ddl.SchemaAnalysis.Application.Indexing;
using AkkornStudio.Ddl.SchemaAnalysis.Application.Rules;
using AkkornStudio.Ddl.SchemaAnalysis.Application.Validation;
using AkkornStudio.Ddl.SchemaAnalysis.Domain.Contracts;
using AkkornStudio.Ddl.SchemaAnalysis.Domain.Enums;
using AkkornStudio.Metadata;

namespace AkkornStudio.Tests.Unit.Ddl.SchemaAnalysis;

public sealed class FkCatalogInconsistentRuleTests
{
    private readonly SchemaMetadataIndexBuilder _indexBuilder = new();
    private readonly FkCatalogInconsistentRule _rule = new();

    [Fact]
    public async Task ExecuteAsync_EmitsIssue_WhenChildColumnDoesNotExist()
    {
        DbMetadata metadata = CreateMetadata(
            childColumnNameInForeignKey: "customer_id",
            childTableColumns: [CreateColumn("id", isPrimaryKey: true)]
        );

        SchemaRuleExecutionResult result = await _rule.ExecuteAsync(CreateContext(metadata));

        Assert.Single(result.Issues);
        Assert.Equal(SchemaIssueSeverity.Critical, result.Issues[0].Severity);
        Assert.Contains(result.Issues[0].Evidence, evidence => evidence.Kind == EvidenceKind.ConstraintTopology);
    }

    [Fact]
    public async Task ExecuteAsync_EmitsIssue_WhenParentColumnDoesNotExist()
    {
        DbMetadata metadata = CreateMetadata(
            parentColumnNameInForeignKey: "id",
            parentTableColumns: [CreateColumn("code", isPrimaryKey: true)]
        );

        SchemaRuleExecutionResult result = await _rule.ExecuteAsync(CreateContext(metadata));

        Assert.Single(result.Issues);
        Assert.Equal(SchemaIssueSeverity.Critical, result.Issues[0].Severity);
    }

    [Fact]
    public async Task ExecuteAsync_EmitsIssue_WhenTypesAreIncompatible()
    {
        DbMetadata metadata = CreateMetadata(
            childTableColumns: [CreateColumn("customer_id", nativeType: "bit")],
            parentTableColumns: [CreateColumn("id", nativeType: "int", isPrimaryKey: true)],
            provider: DatabaseProvider.SqlServer
        );

        SchemaRuleExecutionResult result = await _rule.ExecuteAsync(CreateContext(metadata));

        Assert.Single(result.Issues);
        Assert.Equal(SchemaIssueSeverity.Critical, result.Issues[0].Severity);
        Assert.Contains(result.Issues[0].Evidence, evidence => evidence.Kind == EvidenceKind.TypeCompatibility);
    }

    [Fact]
    public async Task ExecuteAsync_EmitsWarning_WhenCompatibilityIsSemanticWeak()
    {
        DbMetadata metadata = CreateMetadata(
            childTableColumns: [CreateColumn("customer_id", nativeType: "varchar(36)")],
            parentTableColumns: [CreateColumn("id", nativeType: "uuid", isPrimaryKey: true)],
            provider: DatabaseProvider.Postgres
        );

        SchemaRuleExecutionResult result = await _rule.ExecuteAsync(CreateContext(metadata));

        Assert.Single(result.Issues);
        Assert.Equal(SchemaIssueSeverity.Warning, result.Issues[0].Severity);
    }

    [Fact]
    public async Task ExecuteAsync_EmitsMetadataPartial_WhenTableMetadataIsUnavailable()
    {
        DbMetadata metadata = CreateMetadata(removeParentTableFromSchemas: true);

        SchemaRuleExecutionResult result = await _rule.ExecuteAsync(CreateContext(metadata));

        Assert.Empty(result.Issues);
        Assert.Contains(
            result.Diagnostics,
            diagnostic =>
                diagnostic.Code == "ANL-METADATA-PARTIAL"
                && diagnostic.RuleCode == SchemaRuleCode.FK_CATALOG_INCONSISTENT
        );
    }

    [Fact]
    public async Task ExecuteAsync_DoesNotEmit_WhenTypesAreCompatible()
    {
        DbMetadata metadata = CreateMetadata();

        SchemaRuleExecutionResult result = await _rule.ExecuteAsync(CreateContext(metadata));

        Assert.Empty(result.Issues);
        Assert.Empty(result.Diagnostics);
    }

    private SchemaAnalysisExecutionContext CreateContext(DbMetadata metadata)
    {
        SchemaAnalysisProfile profile = SchemaAnalysisProfileNormalizer.CreateDefaultProfile();
        SchemaMetadataIndexSnapshot indices = _indexBuilder.Build(metadata, profile);
        return new SchemaAnalysisExecutionContext(metadata, profile, indices, "fingerprint", "profile");
    }

    private static DbMetadata CreateMetadata(
        string childColumnNameInForeignKey = "customer_id",
        string parentColumnNameInForeignKey = "id",
        IReadOnlyList<ColumnMetadata>? childTableColumns = null,
        IReadOnlyList<ColumnMetadata>? parentTableColumns = null,
        DatabaseProvider provider = DatabaseProvider.Postgres,
        bool removeParentTableFromSchemas = false
    )
    {
        ForeignKeyRelation foreignKey = new(
            ConstraintName: "fk_orders_customer",
            ChildSchema: provider == DatabaseProvider.MySql ? string.Empty : "public",
            ChildTable: "orders",
            ChildColumn: childColumnNameInForeignKey,
            ParentSchema: provider == DatabaseProvider.MySql ? string.Empty : "public",
            ParentTable: "customers",
            ParentColumn: parentColumnNameInForeignKey,
            OnDelete: ReferentialAction.NoAction,
            OnUpdate: ReferentialAction.NoAction
        );

        TableMetadata childTable = new(
            Schema: provider == DatabaseProvider.MySql ? string.Empty : "public",
            Name: "orders",
            Kind: TableKind.Table,
            EstimatedRowCount: 10,
            Columns: childTableColumns ?? [CreateColumn("customer_id", nativeType: "integer")],
            Indexes: [],
            OutboundForeignKeys: [foreignKey],
            InboundForeignKeys: [],
            Comment: "Orders"
        );

        TableMetadata parentTable = new(
            Schema: provider == DatabaseProvider.MySql ? string.Empty : "public",
            Name: "customers",
            Kind: TableKind.Table,
            EstimatedRowCount: 10,
            Columns: parentTableColumns ?? [CreateColumn("id", nativeType: "integer", isPrimaryKey: true)],
            Indexes: [],
            OutboundForeignKeys: [],
            InboundForeignKeys: [foreignKey],
            Comment: "Customers"
        );

        IReadOnlyList<TableMetadata> tables = removeParentTableFromSchemas ? [childTable] : [childTable, parentTable];
        string schemaName = provider == DatabaseProvider.MySql ? string.Empty : "public";

        return new DbMetadata(
            DatabaseName: "db",
            Provider: provider,
            ServerVersion: "1",
            CapturedAt: DateTimeOffset.UtcNow,
            Schemas: [new SchemaMetadata(schemaName, tables)],
            AllForeignKeys: [foreignKey]
        );
    }

    private static ColumnMetadata CreateColumn(
        string name,
        string nativeType = "integer",
        bool isPrimaryKey = false
    ) =>
        new(
            Name: name,
            DataType: nativeType,
            NativeType: nativeType,
            IsNullable: false,
            IsPrimaryKey: isPrimaryKey,
            IsForeignKey: !isPrimaryKey,
            IsUnique: false,
            IsIndexed: false,
            OrdinalPosition: 1,
            Comment: "Column"
        );
}
