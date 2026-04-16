using AkkornStudio.Ddl.SchemaAnalysis.Domain.Normalization;

namespace AkkornStudio.Tests.Unit.Ddl.SchemaAnalysis;

public sealed class SchemaCanonicalizerTests
{
    [Theory]
    [InlineData(DatabaseProvider.Postgres, null, "public")]
    [InlineData(DatabaseProvider.Postgres, "", "public")]
    [InlineData(DatabaseProvider.Postgres, "   ", "public")]
    [InlineData(DatabaseProvider.Postgres, "sales", "sales")]
    [InlineData(DatabaseProvider.SqlServer, null, "dbo")]
    [InlineData(DatabaseProvider.SqlServer, "", "dbo")]
    [InlineData(DatabaseProvider.SqlServer, "   ", "dbo")]
    [InlineData(DatabaseProvider.SqlServer, "custom", "custom")]
    [InlineData(DatabaseProvider.MySql, null, null)]
    [InlineData(DatabaseProvider.MySql, "", null)]
    [InlineData(DatabaseProvider.MySql, "   ", null)]
    [InlineData(DatabaseProvider.MySql, "tenant", null)]
    [InlineData(DatabaseProvider.SQLite, null, "main")]
    [InlineData(DatabaseProvider.SQLite, "", "main")]
    [InlineData(DatabaseProvider.SQLite, "   ", "main")]
    [InlineData(DatabaseProvider.SQLite, "aux", "aux")]
    public void Normalize_ReturnsExpectedCanonicalSchema(
        DatabaseProvider provider,
        string? rawSchema,
        string? expectedCanonical
    )
    {
        string? canonical = SchemaCanonicalizer.Normalize(provider, rawSchema);
        Assert.Equal(expectedCanonical, canonical);
    }

    [Fact]
    public void AreEquivalent_UsesCanonicalComparison_ForPostgresDefaultSchema()
    {
        bool equivalent = SchemaCanonicalizer.AreEquivalent(
            DatabaseProvider.Postgres,
            leftSchema: null,
            rightSchema: "public"
        );

        Assert.True(equivalent);
    }

    [Fact]
    public void AreEquivalent_UsesCanonicalComparison_ForSqlServerDefaultSchema()
    {
        bool equivalent = SchemaCanonicalizer.AreEquivalent(
            DatabaseProvider.SqlServer,
            leftSchema: " ",
            rightSchema: "dbo"
        );

        Assert.True(equivalent);
    }

    [Fact]
    public void AreEquivalent_AlwaysNullCanonicalForMySql()
    {
        bool equivalent = SchemaCanonicalizer.AreEquivalent(
            DatabaseProvider.MySql,
            leftSchema: "tenant_a",
            rightSchema: "tenant_b"
        );

        Assert.True(equivalent);
    }

    [Fact]
    public void AreEquivalent_IsCaseInsensitiveForCanonicalSchemas()
    {
        bool equivalent = SchemaCanonicalizer.AreEquivalent(
            DatabaseProvider.SQLite,
            leftSchema: "MAIN",
            rightSchema: "main"
        );

        Assert.True(equivalent);
    }
}
