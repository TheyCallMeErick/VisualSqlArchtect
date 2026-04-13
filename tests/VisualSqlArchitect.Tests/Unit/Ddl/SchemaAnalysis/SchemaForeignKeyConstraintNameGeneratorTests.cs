using DBWeaver.Ddl.SchemaAnalysis.Application.Processing;

namespace DBWeaver.Tests.Unit.Ddl.SchemaAnalysis;

public sealed class SchemaForeignKeyConstraintNameGeneratorTests
{
    private readonly SchemaForeignKeyConstraintNameGenerator _generator = new();

    [Fact]
    public void Generate_ReturnsSimpleName_WhenNoCollisionExists()
    {
        string? constraintName = _generator.Generate(
            childTable: "orders",
            childColumn: "customer_id",
            parentTable: "customers",
            parentColumn: "id",
            existingConstraintNames: new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        );

        Assert.Equal("fk_orders_customer_id__customers_id", constraintName);
    }

    [Fact]
    public void Generate_TruncatesLongNames_WithHashSuffix()
    {
        string? constraintName = _generator.Generate(
            childTable: "orders_with_really_long_business_context_name",
            childColumn: "customer_identifier_primary_reference",
            parentTable: "customers_with_really_long_business_context_name",
            parentColumn: "identifier_primary_reference",
            existingConstraintNames: new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        );

        Assert.NotNull(constraintName);
        Assert.True(constraintName!.Length <= 63);
        Assert.Matches("^.{1,55}_[0-9a-f]{7}$", constraintName);
    }

    [Fact]
    public void Generate_AppendsVersionSuffix_WhenNameAlreadyExists()
    {
        HashSet<string> existingNames = new(StringComparer.OrdinalIgnoreCase)
        {
            "fk_orders_customer_id__customers_id",
        };

        string? constraintName = _generator.Generate(
            childTable: "orders",
            childColumn: "customer_id",
            parentTable: "customers",
            parentColumn: "id",
            existingConstraintNames: existingNames
        );

        Assert.Equal("fk_orders_customer_id__customers_id_v2", constraintName);
    }

    [Fact]
    public void Generate_ReturnsNull_WhenVersionSuffixesAreExhausted()
    {
        HashSet<string> existingNames = new(StringComparer.OrdinalIgnoreCase)
        {
            "fk_orders_customer_id__customers_id",
        };

        for (int version = 2; version <= 99; version++)
        {
            existingNames.Add($"fk_orders_customer_id__customers_id_v{version}");
        }

        string? constraintName = _generator.Generate(
            childTable: "orders",
            childColumn: "customer_id",
            parentTable: "customers",
            parentColumn: "id",
            existingConstraintNames: existingNames
        );

        Assert.Null(constraintName);
    }
}
