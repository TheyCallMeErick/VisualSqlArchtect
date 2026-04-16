using AkkornStudio.Core;
using AkkornStudio.Ddl.SchemaAnalysis.Application.Processing;

namespace AkkornStudio.Tests.Unit.Ddl.SchemaAnalysis;

public sealed class SchemaPreconditionsSqlGeneratorTests
{
    private readonly SchemaPreconditionsSqlGenerator _generator = new();

    [Fact]
    public void GenerateForeignKeyPreconditions_ReturnsPostgresTemplates()
    {
        IReadOnlyList<string>? preconditions = _generator.GenerateForeignKeyPreconditions(
            CreateRequest(DatabaseProvider.Postgres)
        );

        Assert.NotNull(preconditions);
        Assert.Collection(
            preconditions!,
            item => Assert.Equal("SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'orders'", item),
            item => Assert.Equal("SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'customers'", item),
            item => Assert.Equal("SELECT 1 FROM information_schema.table_constraints WHERE constraint_schema = 'public' AND constraint_name = 'fk_orders_customer_id__customers_id'", item),
            item =>
            {
                Assert.Contains("information_schema.table_constraints", item);
                Assert.Contains("fk.child_columns = 'customer_id'", item);
                Assert.Contains("fk.parent_columns = 'id'", item);
            }
        );
    }

    [Fact]
    public void GenerateForeignKeyPreconditions_ReturnsSqlServerTemplates()
    {
        IReadOnlyList<string>? preconditions = _generator.GenerateForeignKeyPreconditions(
            CreateRequest(DatabaseProvider.SqlServer, "dbo")
        );

        Assert.NotNull(preconditions);
        Assert.Collection(
            preconditions!,
            item => Assert.Equal("SELECT 1 FROM sys.tables t JOIN sys.schemas s ON s.schema_id=t.schema_id WHERE s.name=N'dbo' AND t.name=N'orders'", item),
            item => Assert.Equal("SELECT 1 FROM sys.tables t JOIN sys.schemas s ON s.schema_id=t.schema_id WHERE s.name=N'dbo' AND t.name=N'customers'", item),
            item => Assert.Equal("SELECT 1 FROM sys.foreign_keys WHERE name=N'fk_orders_customer_id__customers_id'", item),
            item =>
            {
                Assert.Contains("sys.foreign_keys fk", item);
                Assert.Contains("fk.child_columns = N'customer_id'", item);
                Assert.Contains("fk.parent_columns = N'id'", item);
            }
        );
    }

    [Fact]
    public void GenerateForeignKeyPreconditions_ReturnsMySqlTemplates()
    {
        IReadOnlyList<string>? preconditions = _generator.GenerateForeignKeyPreconditions(
            CreateRequest(DatabaseProvider.MySql, null)
        );

        Assert.NotNull(preconditions);
        Assert.Collection(
            preconditions!,
            item => Assert.Equal("SELECT 1 FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name = 'orders'", item),
            item => Assert.Equal("SELECT 1 FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name = 'customers'", item),
            item => Assert.Equal("SELECT 1 FROM information_schema.table_constraints WHERE constraint_schema = DATABASE() AND constraint_name = 'fk_orders_customer_id__customers_id'", item),
            item =>
            {
                Assert.Contains("information_schema.key_column_usage", item);
                Assert.Contains("fk.child_columns = 'customer_id'", item);
                Assert.Contains("fk.parent_columns = 'id'", item);
            }
        );
    }

    [Fact]
    public void GenerateForeignKeyPreconditions_ReturnsNull_ForSqlite()
    {
        IReadOnlyList<string>? preconditions = _generator.GenerateForeignKeyPreconditions(
            CreateRequest(DatabaseProvider.SQLite)
        );

        Assert.Null(preconditions);
    }

    private static SchemaForeignKeyPreconditionRequest CreateRequest(
        DatabaseProvider provider,
        string? schemaName = "public"
    )
    {
        return new SchemaForeignKeyPreconditionRequest(
            Provider: provider,
            SchemaName: schemaName,
            ChildTable: "orders",
            ChildColumns: ["customer_id"],
            ParentTable: "customers",
            ParentColumns: ["id"],
            ConstraintName: "fk_orders_customer_id__customers_id"
        );
    }
}
