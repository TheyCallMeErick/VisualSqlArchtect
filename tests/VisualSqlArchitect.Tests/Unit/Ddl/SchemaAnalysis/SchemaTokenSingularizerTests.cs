using DBWeaver.Ddl.SchemaAnalysis.Domain.Normalization;

namespace DBWeaver.Tests.Unit.Ddl.SchemaAnalysis;

public sealed class SchemaTokenSingularizerTests
{
    [Theory]
    [InlineData("categories", "category")]
    [InlineData("wishes", "wish")]
    [InlineData("cases", "cas")]
    [InlineData("orders", "order")]
    public void Singularize_AppliesNormativeRules(string rawToken, string expected)
    {
        string singular = SchemaTokenSingularizer.Singularize(rawToken);

        Assert.Equal(expected, singular);
    }

    [Theory]
    [InlineData("boss")]
    [InlineData("status")]
    [InlineData("")]
    public void Singularize_PreservesNormativeExceptions(string rawToken)
    {
        string singular = SchemaTokenSingularizer.Singularize(rawToken);

        Assert.Equal(rawToken, singular);
    }
}
