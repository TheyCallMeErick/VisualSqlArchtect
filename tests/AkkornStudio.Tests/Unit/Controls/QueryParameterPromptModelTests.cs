using AkkornStudio.UI.Controls.Query;
using AkkornStudio.UI.Services;

namespace AkkornStudio.Tests.Unit.Controls;

public sealed class QueryParameterPromptModelTests
{
    [Fact]
    public void BuildField_PrefersRememberedValueOverSuggestedParameter()
    {
        QueryParameterPlaceholder placeholder = new("@min_id", QueryParameterPlaceholderKind.Named);
        string key = QueryParameterPlaceholderParser.GetStorageKey(placeholder);

        QueryParameterPromptField field = QueryParameterPromptModel.BuildField(
            "SELECT * FROM orders WHERE id >= @min_id",
            placeholder,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [key] = "99",
            },
            new Dictionary<string, QueryParameter>(StringComparer.OrdinalIgnoreCase)
            {
                [key] = new QueryParameter("@min_id", 42),
            },
            new Dictionary<string, QueryExecutionParameterContext>(StringComparer.OrdinalIgnoreCase),
            metadata: null,
            provider: AkkornStudio.Core.DatabaseProvider.Postgres);

        Assert.Equal("99", field.InitialText);
        Assert.Equal(QueryParameterPromptInputKind.Integer, field.InputKind);
    }

    [Fact]
    public void BuildField_UsesSuggestedNullAsInitialNullState()
    {
        QueryParameterPlaceholder placeholder = new("@status", QueryParameterPlaceholderKind.Named);
        string key = QueryParameterPlaceholderParser.GetStorageKey(placeholder);

        QueryParameterPromptField field = QueryParameterPromptModel.BuildField(
            "SELECT * FROM orders WHERE status = @status",
            placeholder,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            new Dictionary<string, QueryParameter>(StringComparer.OrdinalIgnoreCase)
            {
                [key] = new QueryParameter("@status", null),
            },
            new Dictionary<string, QueryExecutionParameterContext>(StringComparer.OrdinalIgnoreCase),
            metadata: null,
            provider: AkkornStudio.Core.DatabaseProvider.Postgres);

        Assert.Equal("NULL", field.InitialText);
        Assert.True(field.StartsAsNull);
        Assert.Equal(QueryParameterPromptInputKind.Text, field.InputKind);
    }

    [Theory]
    [InlineData("boolean", QueryParameterPromptInputKind.Boolean)]
    [InlineData("integer", QueryParameterPromptInputKind.Integer)]
    [InlineData("decimal", QueryParameterPromptInputKind.Decimal)]
    [InlineData("date/time", QueryParameterPromptInputKind.DateTime)]
    [InlineData("text", QueryParameterPromptInputKind.Text)]
    public void ResolveInputKind_MapsHintTypesToPromptControlKinds(
        string typeLabel,
        QueryParameterPromptInputKind expected)
    {
        QueryParameterHint hint = new(typeLabel, "sample", "description");

        Assert.Equal(expected, QueryParameterPromptModel.ResolveInputKind(hint));
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("42", 42)]
    [InlineData("NULL", null)]
    [InlineData("", "")]
    public void ParseInputValue_ConvertsPromptTextToTypedValue(string raw, object? expected)
    {
        object? parsed = QueryParameterPromptModel.ParseInputValue(raw);

        Assert.Equal(expected, parsed);
    }

    [Fact]
    public void ParseInputValue_ParsesDecimalWithInvariantCulture()
    {
        object? parsed = QueryParameterPromptModel.ParseInputValue("19.5");

        Assert.Equal(19.5m, Assert.IsType<decimal>(parsed));
    }

    [Fact]
    public void ParseInputValue_ParsesRoundTripDateTime()
    {
        object? parsed = QueryParameterPromptModel.ParseInputValue("2026-04-22T13:45:00.0000000Z");

        DateTime value = Assert.IsType<DateTime>(parsed);
        Assert.Equal(DateTimeKind.Utc, value.Kind);
        Assert.Equal(new DateTime(2026, 4, 22, 13, 45, 0, DateTimeKind.Utc), value);
    }

    [Fact]
    public void BuildResult_ReturnsNullWhenPromptIsCancelled()
    {
        QueryParameterPlaceholder placeholder = new("@id", QueryParameterPlaceholderKind.Named);

        IReadOnlyList<QueryParameter>? result = QueryParameterPromptModel.BuildResult(
            [placeholder],
            new Dictionary<QueryParameterPlaceholder, string>
            {
                [placeholder] = "42",
            },
            cancelled: true);

        Assert.Null(result);
    }

    [Fact]
    public void BuildResult_UsesNamedAndPositionalParameterBindings()
    {
        QueryParameterPlaceholder named = new("@id", QueryParameterPlaceholderKind.Named);
        QueryParameterPlaceholder positional = new("?", QueryParameterPlaceholderKind.Positional, 1);

        IReadOnlyList<QueryParameter>? result = QueryParameterPromptModel.BuildResult(
            [named, positional],
            new Dictionary<QueryParameterPlaceholder, string>
            {
                [named] = "42",
                [positional] = "NULL",
            },
            cancelled: false);

        Assert.NotNull(result);
        Assert.Equal("@id", result[0].Name);
        Assert.Equal(42, result[0].Value);
        Assert.Null(result[1].Name);
        Assert.Null(result[1].Value);
    }
}
