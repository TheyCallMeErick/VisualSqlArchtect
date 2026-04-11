using DBWeaver.Core;
using DBWeaver.Metadata;
using DBWeaver.UI.Services.SqlEditor;

namespace DBWeaver.Tests.Unit.Services.SqlEditor;

public sealed class SqlHoverDocumentationServiceTests
{
    [Fact]
    public void TryResolve_WithQualifiedColumn_ReturnsColumnDocumentation()
    {
        var sut = new SqlHoverDocumentationService();
        DbMetadata metadata = BuildMetadata();
        const string sql = "SELECT * FROM public.orders o WHERE o.customer_id";
        int caretOffset = sql.IndexOf("o.customer_id", StringComparison.Ordinal) + 3;

        HoverDocumentationInfo? info = sut.TryResolve(sql, caretOffset, metadata, DatabaseProvider.Postgres);

        Assert.NotNull(info);
        Assert.Contains("public.orders.customer_id", info.DisplayText, StringComparison.Ordinal);
        Assert.Contains("FK", info.DisplayText, StringComparison.Ordinal);
    }

    [Fact]
    public void TryResolve_WithTableName_ReturnsTableDocumentation()
    {
        var sut = new SqlHoverDocumentationService();
        DbMetadata metadata = BuildMetadata();
        const string sql = "SELECT * FROM public.orders";
        int caretOffset = sql.IndexOf("public.orders", StringComparison.Ordinal) + 4;

        HoverDocumentationInfo? info = sut.TryResolve(sql, caretOffset, metadata, DatabaseProvider.Postgres);

        Assert.NotNull(info);
        Assert.Contains("public.orders [Table]", info.DisplayText, StringComparison.Ordinal);
    }

    [Fact]
    public void TryResolve_WithoutMetadata_ReturnsNull()
    {
        var sut = new SqlHoverDocumentationService();

        HoverDocumentationInfo? info = sut.TryResolve("SELECT * FROM public.orders", 10, metadata: null, DatabaseProvider.Postgres);

        Assert.Null(info);
    }

    private static DbMetadata BuildMetadata()
    {
        var fk = new ForeignKeyRelation(
            ConstraintName: "fk_orders_customers",
            ChildSchema: "public",
            ChildTable: "orders",
            ChildColumn: "customer_id",
            ParentSchema: "public",
            ParentTable: "customers",
            ParentColumn: "id",
            OnDelete: ReferentialAction.NoAction,
            OnUpdate: ReferentialAction.NoAction,
            OrdinalPosition: 1);

        var orders = new TableMetadata(
            Schema: "public",
            Name: "orders",
            Kind: TableKind.Table,
            EstimatedRowCount: 1000,
            Columns:
            [
                new ColumnMetadata("id", "int", "int", false, true, false, true, true, 1),
                new ColumnMetadata("customer_id", "int", "int", true, false, true, false, true, 2),
            ],
            Indexes: [],
            OutboundForeignKeys: [fk],
            InboundForeignKeys: []);

        var customers = new TableMetadata(
            Schema: "public",
            Name: "customers",
            Kind: TableKind.Table,
            EstimatedRowCount: 200,
            Columns:
            [
                new ColumnMetadata("id", "int", "int", false, true, false, true, true, 1),
            ],
            Indexes: [],
            OutboundForeignKeys: [],
            InboundForeignKeys: [fk]);

        return new DbMetadata(
            DatabaseName: "dbweaver",
            Provider: DatabaseProvider.Postgres,
            ServerVersion: "16.0",
            CapturedAt: DateTimeOffset.UtcNow,
            Schemas:
            [
                new SchemaMetadata("public", [orders, customers]),
            ],
            AllForeignKeys: [fk]);
    }
}
