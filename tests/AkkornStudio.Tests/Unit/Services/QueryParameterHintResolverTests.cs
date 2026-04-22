using AkkornStudio.UI.Services;

namespace AkkornStudio.Tests.Unit.Services;

public sealed class QueryParameterHintResolverTests
{
    [Fact]
    public void Resolve_InfersIntegerForIdParameter()
    {
        QueryParameterHint hint = QueryParameterHintResolver.Resolve(
            "SELECT * FROM users WHERE customer_id = @customer_id",
            new QueryParameterPlaceholder("@customer_id", QueryParameterPlaceholderKind.Named));

        Assert.Equal("integer", hint.TypeLabel);
        Assert.Equal("42", hint.ExampleValue);
    }

    [Fact]
    public void Resolve_InfersBooleanForActiveParameter()
    {
        QueryParameterHint hint = QueryParameterHintResolver.Resolve(
            "SELECT * FROM users WHERE is_active = @is_active",
            new QueryParameterPlaceholder("@is_active", QueryParameterPlaceholderKind.Named));

        Assert.Equal("boolean", hint.TypeLabel);
        Assert.Equal("true", hint.ExampleValue);
    }

    [Fact]
    public void Resolve_InfersDateForDateParameter()
    {
        QueryParameterHint hint = QueryParameterHintResolver.Resolve(
            "SELECT * FROM orders WHERE created_at >= :start_date",
            new QueryParameterPlaceholder(":start_date", QueryParameterPlaceholderKind.Named));

        Assert.Equal("date/time", hint.TypeLabel);
        Assert.Equal("2026-01-31", hint.ExampleValue);
    }

    [Fact]
    public void Resolve_InfersTextForLikeContext()
    {
        QueryParameterHint hint = QueryParameterHintResolver.Resolve(
            "SELECT * FROM users WHERE name LIKE ?",
            new QueryParameterPlaceholder("?", QueryParameterPlaceholderKind.Positional, 1));

        Assert.Equal("text", hint.TypeLabel);
        Assert.Equal("sample", hint.ExampleValue);
    }

    [Fact]
    public void Resolve_PrefersSuggestedIntegerBindingFromVisualPipeline()
    {
        QueryParameterHint hint = QueryParameterHintResolver.Resolve(
            "SELECT * FROM users WHERE customer_id = @customer_id",
            new QueryParameterPlaceholder("@customer_id", QueryParameterPlaceholderKind.Named),
            new QueryParameter("@customer_id", 108));

        Assert.Equal("integer", hint.TypeLabel);
        Assert.Equal("108", hint.ExampleValue);
        Assert.Contains("pipeline visual", hint.Description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("@customer_id", hint.ContextLabel, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Resolve_PrefersSuggestedDateTimeBindingFromVisualPipeline()
    {
        DateTime value = new(2026, 4, 22, 13, 45, 0, DateTimeKind.Utc);
        QueryParameterHint hint = QueryParameterHintResolver.Resolve(
            "SELECT * FROM orders WHERE created_at >= :start_date",
            new QueryParameterPlaceholder(":start_date", QueryParameterPlaceholderKind.Named),
            new QueryParameter(":start_date", value));

        Assert.Equal("date/time", hint.TypeLabel);
        Assert.Equal("2026-04-22T13:45:00.0000000Z", hint.ExampleValue);
        Assert.Contains(":start_date", hint.ContextLabel, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Resolve_ExtractsSourceReferenceFromSqlContext()
    {
        QueryParameterHint hint = QueryParameterHintResolver.Resolve(
            "SELECT * FROM orders o WHERE o.customer_id = @p0",
            new QueryParameterPlaceholder("@p0", QueryParameterPlaceholderKind.Named));

        Assert.Contains("o.customer_id", hint.ContextLabel, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Resolve_CombinesVisualBindingAndSqlSourceContext()
    {
        QueryParameterHint hint = QueryParameterHintResolver.Resolve(
            "SELECT * FROM orders o WHERE o.customer_id = @p0",
            new QueryParameterPlaceholder("@p0", QueryParameterPlaceholderKind.Named),
            new QueryParameter("@p0", 108));

        Assert.Contains("@p0", hint.ContextLabel, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("o.customer_id", hint.ContextLabel, StringComparison.OrdinalIgnoreCase);
    }
}
