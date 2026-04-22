using System.Globalization;
using System.Text.RegularExpressions;

namespace AkkornStudio.UI.Services;

internal static partial class QueryParameterHintResolver
{
    public static QueryParameterHint Resolve(
        string sql,
        QueryParameterPlaceholder placeholder,
        QueryParameter? suggestedParameter = null)
    {
        if (TryResolveFromSuggestedValue(placeholder, sql, suggestedParameter, out QueryParameterHint suggestedHint))
            return suggestedHint;

        string token = placeholder.Token;
        string normalizedName = QueryParameterPlaceholderParser.NormalizeName(token);
        string sqlUpper = sql.ToUpperInvariant();
        int tokenIndex = sql.IndexOf(token, StringComparison.OrdinalIgnoreCase);
        string context = tokenIndex >= 0
            ? sqlUpper[Math.Max(0, tokenIndex - 32)..Math.Min(sqlUpper.Length, tokenIndex + token.Length + 32)]
            : sqlUpper;
        string nameUpper = normalizedName.ToUpperInvariant();

        if (nameUpper.Contains("DATE", StringComparison.Ordinal)
            || nameUpper.Contains("TIME", StringComparison.Ordinal)
            || context.Contains("DATE", StringComparison.Ordinal)
            || context.Contains("TIMESTAMP", StringComparison.Ordinal))
        {
            return new QueryParameterHint("date/time", "2026-01-31", "Accepts ISO date/time text.", BuildContextLabel(sql, placeholder, null));
        }

        if (nameUpper.StartsWith("IS_", StringComparison.Ordinal)
            || nameUpper.StartsWith("HAS_", StringComparison.Ordinal)
            || nameUpper.Contains("ENABLED", StringComparison.Ordinal)
            || nameUpper.Contains("ACTIVE", StringComparison.Ordinal)
            || context.Contains(" = TRUE", StringComparison.Ordinal)
            || context.Contains(" = FALSE", StringComparison.Ordinal))
        {
            return new QueryParameterHint("boolean", "true", "Use true or false.", BuildContextLabel(sql, placeholder, null));
        }

        if (nameUpper.EndsWith("_ID", StringComparison.Ordinal)
            || nameUpper.Equals("ID", StringComparison.Ordinal)
            || nameUpper.Contains("COUNT", StringComparison.Ordinal)
            || nameUpper.Contains("LIMIT", StringComparison.Ordinal)
            || nameUpper.Contains("OFFSET", StringComparison.Ordinal)
            || context.Contains(" LIMIT ", StringComparison.Ordinal)
            || context.Contains(" OFFSET ", StringComparison.Ordinal))
        {
            return new QueryParameterHint("integer", "42", "Whole numeric value.", BuildContextLabel(sql, placeholder, null));
        }

        if (nameUpper.Contains("PRICE", StringComparison.Ordinal)
            || nameUpper.Contains("AMOUNT", StringComparison.Ordinal)
            || nameUpper.Contains("TOTAL", StringComparison.Ordinal)
            || context.Contains("DECIMAL", StringComparison.Ordinal)
            || context.Contains("NUMERIC", StringComparison.Ordinal))
        {
            return new QueryParameterHint("decimal", "19.99", "Decimal numeric value.", BuildContextLabel(sql, placeholder, null));
        }

        if (context.Contains(" LIKE ", StringComparison.Ordinal)
            || nameUpper.Contains("NAME", StringComparison.Ordinal)
            || nameUpper.Contains("EMAIL", StringComparison.Ordinal)
            || nameUpper.Contains("STATUS", StringComparison.Ordinal)
            || nameUpper.Contains("CODE", StringComparison.Ordinal))
        {
            return new QueryParameterHint("text", "sample", "Plain text value.", BuildContextLabel(sql, placeholder, null));
        }

        return new QueryParameterHint("text", "value", "Value inferred as generic text.", BuildContextLabel(sql, placeholder, null));
    }

    private static bool TryResolveFromSuggestedValue(
        QueryParameterPlaceholder placeholder,
        string sql,
        QueryParameter? suggestedParameter,
        out QueryParameterHint hint)
    {
        if (suggestedParameter is null)
        {
            hint = null!;
            return false;
        }

        string bindingLabel = !string.IsNullOrWhiteSpace(suggestedParameter.Name)
            ? suggestedParameter.Name!
            : placeholder.Token;
        string contextLabel = BuildContextLabel(sql, placeholder, bindingLabel);

        if (suggestedParameter.Value is null)
        {
            hint = new QueryParameterHint("null", "null", "Valor atual nulo no pipeline visual.", contextLabel);
            return true;
        }

        switch (suggestedParameter.Value)
        {
            case bool boolValue:
                hint = new QueryParameterHint(
                    "boolean",
                    boolValue ? "true" : "false",
                    "Tipo inferido a partir do binding real do pipeline visual.",
                    contextLabel);
                return true;
            case sbyte or byte or short or ushort or int or uint or long or ulong:
                hint = new QueryParameterHint(
                    "integer",
                    Convert.ToString(suggestedParameter.Value, CultureInfo.InvariantCulture) ?? "42",
                    "Tipo inferido a partir do binding real do pipeline visual.",
                    contextLabel);
                return true;
            case float or double or decimal:
                hint = new QueryParameterHint(
                    "decimal",
                    Convert.ToString(suggestedParameter.Value, CultureInfo.InvariantCulture) ?? "19.99",
                    "Tipo inferido a partir do binding real do pipeline visual.",
                    contextLabel);
                return true;
            case DateTime dateTime:
                hint = new QueryParameterHint(
                    "date/time",
                    dateTime.ToString("O", CultureInfo.InvariantCulture),
                    "Tipo inferido a partir do binding real do pipeline visual.",
                    contextLabel);
                return true;
            case DateTimeOffset dateTimeOffset:
                hint = new QueryParameterHint(
                    "date/time",
                    dateTimeOffset.ToString("O", CultureInfo.InvariantCulture),
                    "Tipo inferido a partir do binding real do pipeline visual.",
                    contextLabel);
                return true;
            case Guid guid:
                hint = new QueryParameterHint(
                    "uuid",
                    guid.ToString(),
                    "Tipo inferido a partir do binding real do pipeline visual.",
                    contextLabel);
                return true;
            case string stringValue:
                hint = new QueryParameterHint(
                    "text",
                    string.IsNullOrWhiteSpace(stringValue) ? "value" : stringValue,
                    "Valor inicial vindo do pipeline visual.",
                    contextLabel);
                return true;
            default:
                hint = new QueryParameterHint(
                    suggestedParameter.Value.GetType().Name.ToLowerInvariant(),
                    Convert.ToString(suggestedParameter.Value, CultureInfo.InvariantCulture) ?? "value",
                    "Tipo inferido a partir do binding real do pipeline visual.",
                    contextLabel);
                return true;
        }
    }

    private static string? TryResolveSourceReference(string sql, QueryParameterPlaceholder placeholder)
    {
        int tokenIndex = sql.IndexOf(placeholder.Token, StringComparison.OrdinalIgnoreCase);
        if (tokenIndex < 0)
            return null;

        int windowStart = Math.Max(0, tokenIndex - 120);
        int windowLength = tokenIndex - windowStart;
        if (windowLength <= 0)
            return null;

        string leftWindow = sql.Substring(windowStart, windowLength);
        Match match = SourceReferenceRegex().Match(leftWindow);
        if (!match.Success)
            return null;

        string alias = NormalizeIdentifier(match.Groups["alias"].Value);
        string column = NormalizeIdentifier(match.Groups["column"].Value);
        if (string.IsNullOrWhiteSpace(alias) || string.IsNullOrWhiteSpace(column))
            return null;

        return $"{alias}.{column}";
    }

    private static string BuildContextLabel(string sql, QueryParameterPlaceholder placeholder, string? bindingLabel)
    {
        List<string> parts = [];
        if (!string.IsNullOrWhiteSpace(bindingLabel))
            parts.Add($"Binding do pipeline visual: {bindingLabel}");

        string? sourceReference = TryResolveSourceReference(sql, placeholder);
        if (!string.IsNullOrWhiteSpace(sourceReference))
            parts.Add($"Origem SQL: {sourceReference}");

        return string.Join(" | ", parts);
    }

    private static string NormalizeIdentifier(string raw)
    {
        string trimmed = raw.Trim();
        if (trimmed.Length >= 2)
        {
            if ((trimmed[0] == '"' && trimmed[^1] == '"')
                || (trimmed[0] == '[' && trimmed[^1] == ']')
                || (trimmed[0] == '`' && trimmed[^1] == '`'))
            {
                return trimmed[1..^1];
            }
        }

        return trimmed;
    }

    [GeneratedRegex(@"(?<alias>(?:\[[^\]]+\]|`[^`]+`|""[^""]+""|[A-Za-z_][A-Za-z0-9_]*))\s*\.\s*(?<column>(?:\[[^\]]+\]|`[^`]+`|""[^""]+""|[A-Za-z_][A-Za-z0-9_]*))\s*(?:=|<>|!=|>=|<=|>|<|LIKE|NOT\s+LIKE|IN|NOT\s+IN)\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SourceReferenceRegex();
}
