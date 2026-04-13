using DBWeaver.Ddl.SchemaAnalysis.Domain.Normalization;

namespace DBWeaver.Tests.Unit.Ddl.SchemaAnalysis;

public sealed class SchemaTypeCompatibilityResolverTests
{
    [Theory]
    [InlineData("int", DatabaseProvider.Postgres, SchemaCanonicalTypeCategory.Integer)]
    [InlineData("numeric", DatabaseProvider.Postgres, SchemaCanonicalTypeCategory.Decimal)]
    [InlineData("varchar", DatabaseProvider.Postgres, SchemaCanonicalTypeCategory.String)]
    [InlineData("timestamp", DatabaseProvider.Postgres, SchemaCanonicalTypeCategory.DateTime)]
    [InlineData("boolean", DatabaseProvider.Postgres, SchemaCanonicalTypeCategory.Boolean)]
    [InlineData("uuid", DatabaseProvider.Postgres, SchemaCanonicalTypeCategory.Guid)]
    [InlineData("bytea", DatabaseProvider.Postgres, SchemaCanonicalTypeCategory.Binary)]
    [InlineData("jsonb", DatabaseProvider.Postgres, SchemaCanonicalTypeCategory.JsonXml)]
    [InlineData("geography", DatabaseProvider.Postgres, SchemaCanonicalTypeCategory.Other)]
    public void GetCanonicalCategory_MapsTypesToNormativeCategories(
        string rawType,
        DatabaseProvider provider,
        SchemaCanonicalTypeCategory expectedCategory
    )
    {
        SchemaTypeCompatibilityResolver resolver = new();

        SchemaCanonicalTypeCategory category = resolver.GetCanonicalCategory(rawType, provider);

        Assert.Equal(expectedCategory, category);
    }

    [Fact]
    public void GetCompatibility_ClassifiesUuidAndVarchar36_AsSemanticWeak()
    {
        SchemaTypeCompatibilityResolver resolver = new();

        SchemaTypeCompatibility compatibility = resolver.GetCompatibility(
            "varchar(36)",
            "uuid",
            DatabaseProvider.Postgres
        );

        Assert.Equal(SchemaTypeCompatibilityLevel.SemanticWeak, compatibility.CompatibilityLevel);
    }

    [Fact]
    public void GetCompatibility_ClassifiesNumericScaleZeroAndInteger_AsSemanticWeak()
    {
        SchemaTypeCompatibilityResolver resolver = new();

        SchemaTypeCompatibility compatibility = resolver.GetCompatibility(
            "numeric(10,0)",
            "int",
            DatabaseProvider.Postgres
        );

        Assert.Equal(SchemaTypeCompatibilityLevel.SemanticWeak, compatibility.CompatibilityLevel);
    }

    [Fact]
    public void GetCanonicalCategory_MapsMySqlTinyIntOne_ToBoolean()
    {
        SchemaTypeCompatibilityResolver resolver = new();

        SchemaCanonicalTypeCategory category = resolver.GetCanonicalCategory(
            "tinyint(1)",
            DatabaseProvider.MySql
        );

        Assert.Equal(SchemaCanonicalTypeCategory.Boolean, category);
    }

    [Fact]
    public void GetCompatibility_TreatsMySqlTinyIntOneAndInteger_AsIncompatible()
    {
        SchemaTypeCompatibilityResolver resolver = new();

        SchemaTypeCompatibility compatibility = resolver.GetCompatibility(
            "tinyint(1)",
            "int",
            DatabaseProvider.MySql
        );

        Assert.Equal(SchemaTypeCompatibilityLevel.Incompatible, compatibility.CompatibilityLevel);
    }

    [Fact]
    public void GetCanonicalCategory_MapsSqlServerBit_ToBoolean()
    {
        SchemaTypeCompatibilityResolver resolver = new();

        SchemaCanonicalTypeCategory category = resolver.GetCanonicalCategory(
            "bit",
            DatabaseProvider.SqlServer
        );

        Assert.Equal(SchemaCanonicalTypeCategory.Boolean, category);
    }

    [Fact]
    public void GetCompatibility_TreatsSqlServerBitAndInteger_AsIncompatible()
    {
        SchemaTypeCompatibilityResolver resolver = new();

        SchemaTypeCompatibility compatibility = resolver.GetCompatibility(
            "bit",
            "int",
            DatabaseProvider.SqlServer
        );

        Assert.Equal(SchemaTypeCompatibilityLevel.Incompatible, compatibility.CompatibilityLevel);
    }

    [Fact]
    public void GetCompatibility_ReturnsExact_ForNormalizedEquivalentTypes()
    {
        SchemaTypeCompatibilityResolver resolver = new();

        SchemaTypeCompatibility compatibility = resolver.GetCompatibility(
            "integer",
            "int",
            DatabaseProvider.Postgres
        );

        Assert.Equal(SchemaTypeCompatibilityLevel.Exact, compatibility.CompatibilityLevel);
        Assert.Equal("integer", compatibility.LeftNormalizedType);
        Assert.Equal("integer", compatibility.RightNormalizedType);
    }

    [Fact]
    public void GetCompatibility_ReturnsSemanticStrong_ForSameCanonicalCategory()
    {
        SchemaTypeCompatibilityResolver resolver = new();

        SchemaTypeCompatibility compatibility = resolver.GetCompatibility(
            "varchar(50)",
            "text",
            DatabaseProvider.Postgres
        );

        Assert.Equal(SchemaTypeCompatibilityLevel.SemanticStrong, compatibility.CompatibilityLevel);
    }
}
