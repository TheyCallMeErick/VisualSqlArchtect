using DBWeaver.Core;
using DBWeaver.Ddl.SchemaAnalysis.Infrastructure.Hashing;
using DBWeaver.Ddl.SchemaAnalysis.Application.Validation;
using DBWeaver.Ddl.SchemaAnalysis.Domain.Contracts;
using DBWeaver.Metadata;

namespace DBWeaver.Tests.Unit.Ddl.SchemaAnalysis;

public sealed class SchemaAnalysisHashingServiceTests
{
    private readonly SchemaAnalysisHashingService _service = new();

    [Fact]
    public void ComputeMetadataFingerprint_ReturnsSameHash_ForIdenticalMetadata()
    {
        DbMetadata metadata = CreateMetadata();

        string first = _service.ComputeMetadataFingerprint(metadata);
        string second = _service.ComputeMetadataFingerprint(metadata);

        Assert.Equal(first, second);
    }

    [Fact]
    public void ComputeMetadataFingerprint_Changes_WhenColumnStructureChanges()
    {
        DbMetadata baseline = CreateMetadata();
        DbMetadata changed = CreateMetadata(
            customerIdColumn: new ColumnMetadata("customer_id", "bigint", "bigint", false, false, true, false, true, 2, Comment: "Customer")
        );

        string first = _service.ComputeMetadataFingerprint(baseline);
        string second = _service.ComputeMetadataFingerprint(changed);

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void ComputeMetadataFingerprint_Changes_WhenForeignKeyChanges()
    {
        DbMetadata baseline = CreateMetadata();
        DbMetadata changed = CreateMetadata(
            foreignKey: new ForeignKeyRelation(
                ConstraintName: "fk_orders_account",
                ChildSchema: "public",
                ChildTable: "orders",
                ChildColumn: "customer_id",
                ParentSchema: "public",
                ParentTable: "accounts",
                ParentColumn: "id",
                OnDelete: ReferentialAction.NoAction,
                OnUpdate: ReferentialAction.NoAction
            ),
            parentTableName: "accounts"
        );

        string first = _service.ComputeMetadataFingerprint(baseline);
        string second = _service.ComputeMetadataFingerprint(changed);

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void ComputeProfileContentHash_ReturnsSameHash_ForIdenticalValidatedProfile()
    {
        SchemaAnalysisProfile profile = SchemaAnalysisProfileNormalizer.CreateDefaultProfile();

        string first = _service.ComputeProfileContentHash(profile);
        string second = _service.ComputeProfileContentHash(profile);

        Assert.Equal(first, second);
    }

    [Fact]
    public void ComputeProfileContentHash_Changes_WhenThresholdChanges()
    {
        SchemaAnalysisProfile baseline = SchemaAnalysisProfileNormalizer.CreateDefaultProfile();
        SchemaAnalysisProfile changed = baseline with
        {
            MinConfidenceGlobal = 0.60,
        };

        string first = _service.ComputeProfileContentHash(baseline);
        string second = _service.ComputeProfileContentHash(changed);

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void ComputeProfileContentHash_Changes_WhenAllowlistChanges()
    {
        SchemaAnalysisProfile baseline = SchemaAnalysisProfileNormalizer.CreateDefaultProfile();
        SchemaAnalysisProfile changed = baseline with
        {
            NameAllowlist = ["customer_name"],
        };

        string first = _service.ComputeProfileContentHash(baseline);
        string second = _service.ComputeProfileContentHash(changed);

        Assert.NotEqual(first, second);
    }

    private static DbMetadata CreateMetadata(
        ColumnMetadata? customerIdColumn = null,
        ForeignKeyRelation? foreignKey = null,
        string parentTableName = "customers"
    )
    {
        ForeignKeyRelation effectiveForeignKey = foreignKey ?? new ForeignKeyRelation(
            ConstraintName: "fk_orders_customer",
            ChildSchema: "public",
            ChildTable: "orders",
            ChildColumn: "customer_id",
            ParentSchema: "public",
            ParentTable: parentTableName,
            ParentColumn: "id",
            OnDelete: ReferentialAction.NoAction,
            OnUpdate: ReferentialAction.NoAction
        );

        TableMetadata orders = new(
            Schema: "public",
            Name: "orders",
            Kind: TableKind.Table,
            EstimatedRowCount: 100,
            Columns:
            [
                new ColumnMetadata("id", "integer", "integer", false, true, false, false, true, 1, Comment: "Order id"),
                customerIdColumn ?? new ColumnMetadata("customer_id", "integer", "integer", false, false, true, false, true, 2, Comment: "Customer"),
            ],
            Indexes:
            [
                new IndexMetadata("pk_orders", true, true, true, ["id"]),
                new IndexMetadata("ix_orders_customer_id", false, false, false, ["customer_id"]),
            ],
            OutboundForeignKeys: [effectiveForeignKey],
            InboundForeignKeys: [],
            Comment: "Orders"
        );

        TableMetadata parent = new(
            Schema: "public",
            Name: parentTableName,
            Kind: TableKind.Table,
            EstimatedRowCount: 10,
            Columns:
            [
                new ColumnMetadata("id", "integer", "integer", false, true, false, false, true, 1, Comment: "Parent id"),
            ],
            Indexes:
            [
                new IndexMetadata($"pk_{parentTableName}", true, true, true, ["id"]),
            ],
            OutboundForeignKeys: [],
            InboundForeignKeys: [effectiveForeignKey],
            Comment: "Parent"
        );

        return new DbMetadata(
            DatabaseName: "db",
            Provider: DatabaseProvider.Postgres,
            ServerVersion: "16",
            CapturedAt: DateTimeOffset.UtcNow,
            Schemas: [new SchemaMetadata("public", [orders, parent])],
            AllForeignKeys: [effectiveForeignKey]
        );
    }
}
