using DBWeaver.Ddl.SchemaAnalysis.Domain.Enums;
using DBWeaver.Ddl.SchemaAnalysis.Domain.Normalization;

namespace DBWeaver.Tests.Unit.Ddl.SchemaAnalysis;

public sealed class SchemaNamingConventionValidatorTests
{
    [Theory]
    [InlineData(NamingConvention.SnakeCase, "customer_id")]
    [InlineData(NamingConvention.CamelCase, "customerId")]
    [InlineData(NamingConvention.PascalCase, "CustomerId")]
    [InlineData(NamingConvention.KebabCase, "customer-id")]
    [InlineData(NamingConvention.MixedAllowed, "Customer_Id")]
    public void IsValid_AcceptsNamesThatMatchConfiguredConvention(
        NamingConvention namingConvention,
        string rawName
    )
    {
        SchemaNamingConventionValidator validator = new();

        bool isValid = validator.IsValid(rawName, namingConvention);

        Assert.True(isValid);
    }

    [Theory]
    [InlineData(NamingConvention.SnakeCase)]
    [InlineData(NamingConvention.CamelCase)]
    [InlineData(NamingConvention.PascalCase)]
    [InlineData(NamingConvention.KebabCase)]
    [InlineData(NamingConvention.MixedAllowed)]
    public void IsValid_RejectsNamesThatStartWithDigits_InAllConventions(
        NamingConvention namingConvention
    )
    {
        SchemaNamingConventionValidator validator = new();

        bool isValid = validator.IsValid("1customer", namingConvention);

        Assert.False(isValid);
    }

    [Theory]
    [InlineData(NamingConvention.SnakeCase, "customer$id")]
    [InlineData(NamingConvention.CamelCase, "customer$id")]
    [InlineData(NamingConvention.PascalCase, "Customer$id")]
    [InlineData(NamingConvention.KebabCase, "customer$id")]
    [InlineData(NamingConvention.MixedAllowed, "customer$id")]
    public void IsValid_RejectsNamesWithResidualInvalidCharacters(
        NamingConvention namingConvention,
        string rawName
    )
    {
        SchemaNamingConventionValidator validator = new();

        bool isValid = validator.IsValid(rawName, namingConvention);

        Assert.False(isValid);
    }
}
