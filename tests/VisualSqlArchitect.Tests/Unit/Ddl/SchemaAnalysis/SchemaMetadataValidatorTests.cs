using DBWeaver.Ddl.SchemaAnalysis.Application.Validation;
using DBWeaver.Ddl.SchemaAnalysis.Domain.Enums;
using DBWeaver.Metadata;

namespace DBWeaver.Tests.Unit.Ddl.SchemaAnalysis;

public sealed class SchemaMetadataValidatorTests
{
    [Fact]
    public void Validate_ReturnsFatalDiagnostic_WhenRequiredRawTypeIsMissing()
    {
        SchemaMetadataValidator validator = new();
        DbMetadata metadata = CreateMetadata(
            CreateTable(
                CreateColumn("customer_id", nativeType: "")
            )
        );

        SchemaMetadataValidationResult result = validator.Validate(metadata);

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Diagnostics,
            diagnostic => diagnostic.Code == "ANL-METADATA-INVALID" && diagnostic.IsFatal
        );
    }

    [Fact]
    public void Validate_EmitsPartialDiagnostic_WhenCommentMetadataIsUnavailable()
    {
        SchemaMetadataValidator validator = new();
        DbMetadata metadata = CreateMetadata(
            CreateTable(
                CreateColumn("id", comment: null),
                tableComment: null
            )
        );

        SchemaMetadataValidationResult result = validator.Validate(metadata);

        Assert.True(result.IsValid);
        Assert.Contains(
            result.Diagnostics,
            diagnostic =>
                diagnostic.Code == "ANL-METADATA-PARTIAL"
                && diagnostic.RuleCode == SchemaRuleCode.MISSING_REQUIRED_COMMENT
        );
    }

    [Fact]
    public void Validate_EmitsPartialDiagnostic_WhenIndexAndUniqueMetadataIsUnavailable()
    {
        SchemaMetadataValidator validator = new();
        DbMetadata metadata = CreateMetadata(
            CreateTable(
                CreateColumn("customer_id", isIndexed: true, isUnique: false),
                indexes: []
            )
        );

        SchemaMetadataValidationResult result = validator.Validate(metadata);

        Assert.True(result.IsValid);
        Assert.Contains(
            result.Diagnostics,
            diagnostic =>
                diagnostic.Code == "ANL-METADATA-PARTIAL"
                && diagnostic.RuleCode == SchemaRuleCode.MISSING_FK
        );
    }

    private static DbMetadata CreateMetadata(TableMetadata table) =>
        new(
            DatabaseName: "db",
            Provider: DatabaseProvider.Postgres,
            ServerVersion: "16",
            CapturedAt: DateTimeOffset.UtcNow,
            Schemas: [new SchemaMetadata("public", [table])],
            AllForeignKeys: []
        );

    private static TableMetadata CreateTable(
        ColumnMetadata column,
        string? tableComment = "Orders",
        IReadOnlyList<IndexMetadata>? indexes = null
    ) =>
        new(
            Schema: "public",
            Name: "orders",
            Kind: TableKind.Table,
            EstimatedRowCount: 120,
            Columns: [column],
            Indexes: indexes ?? [new IndexMetadata("ix_orders_customer_id", false, false, false, ["customer_id"])],
            OutboundForeignKeys: [],
            InboundForeignKeys: [],
            Comment: tableComment
        );

    private static ColumnMetadata CreateColumn(
        string name,
        string nativeType = "integer",
        string? comment = "Customer reference",
        bool isIndexed = false,
        bool isUnique = false
    ) =>
        new(
            Name: name,
            DataType: nativeType,
            NativeType: nativeType,
            IsNullable: false,
            IsPrimaryKey: false,
            IsForeignKey: false,
            IsUnique: isUnique,
            IsIndexed: isIndexed,
            OrdinalPosition: 1,
            Comment: comment
        );
}
