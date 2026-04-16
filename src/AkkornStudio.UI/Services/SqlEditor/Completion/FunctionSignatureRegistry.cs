using AkkornStudio.Core;

namespace AkkornStudio.UI.Services.SqlEditor;

public sealed class FunctionSignatureRegistry
{
    private readonly IReadOnlyDictionary<DatabaseProvider, IReadOnlyDictionary<string, FunctionSignature>> _catalog;

    public FunctionSignatureRegistry()
    {
        _catalog = new Dictionary<DatabaseProvider, IReadOnlyDictionary<string, FunctionSignature>>
        {
            [DatabaseProvider.Postgres] = BuildPostgres(),
            [DatabaseProvider.MySql] = BuildMySql(),
            [DatabaseProvider.SqlServer] = BuildSqlServer(),
            [DatabaseProvider.SQLite] = BuildSQLite(),
        };
    }

    public FunctionSignature? TryResolve(DatabaseProvider provider, string functionName)
    {
        if (string.IsNullOrWhiteSpace(functionName))
            return null;

        if (!_catalog.TryGetValue(provider, out IReadOnlyDictionary<string, FunctionSignature>? providerMap))
            return null;

        return providerMap.TryGetValue(functionName.Trim(), out FunctionSignature? signature)
            ? signature
            : null;
    }

    private static IReadOnlyDictionary<string, FunctionSignature> BuildPostgres()
    {
        return new Dictionary<string, FunctionSignature>(StringComparer.OrdinalIgnoreCase)
        {
            ["NOW"] = new FunctionSignature("NOW", [], "timestamp with time zone"),
            ["DATE_TRUNC"] = new FunctionSignature(
                "DATE_TRUNC",
                [
                    new FunctionParameterSignature("field", "text"),
                    new FunctionParameterSignature("source", "timestamp"),
                ],
                "timestamp"),
            ["COALESCE"] = new FunctionSignature(
                "COALESCE",
                [
                    new FunctionParameterSignature("value_1", "any"),
                    new FunctionParameterSignature("value_2", "any"),
                ],
                "any"),
            ["STRING_AGG"] = new FunctionSignature(
                "STRING_AGG",
                [
                    new FunctionParameterSignature("value", "text"),
                    new FunctionParameterSignature("delimiter", "text"),
                ],
                "text"),
            ["JSONB_EXTRACT_PATH_TEXT"] = new FunctionSignature(
                "JSONB_EXTRACT_PATH_TEXT",
                [
                    new FunctionParameterSignature("from_json", "jsonb"),
                    new FunctionParameterSignature("path", "text"),
                ],
                "text"),
        };
    }

    private static IReadOnlyDictionary<string, FunctionSignature> BuildMySql()
    {
        return new Dictionary<string, FunctionSignature>(StringComparer.OrdinalIgnoreCase)
        {
            ["NOW"] = new FunctionSignature("NOW", [], "datetime"),
            ["DATE_FORMAT"] = new FunctionSignature(
                "DATE_FORMAT",
                [
                    new FunctionParameterSignature("date", "datetime"),
                    new FunctionParameterSignature("format", "text"),
                ],
                "text"),
            ["IFNULL"] = new FunctionSignature(
                "IFNULL",
                [
                    new FunctionParameterSignature("value", "any"),
                    new FunctionParameterSignature("fallback", "any"),
                ],
                "any"),
            ["GROUP_CONCAT"] = new FunctionSignature(
                "GROUP_CONCAT",
                [
                    new FunctionParameterSignature("value", "text"),
                    new FunctionParameterSignature("separator", "text"),
                ],
                "text"),
            ["JSON_EXTRACT"] = new FunctionSignature(
                "JSON_EXTRACT",
                [
                    new FunctionParameterSignature("doc", "json"),
                    new FunctionParameterSignature("path", "text"),
                ],
                "json"),
        };
    }

    private static IReadOnlyDictionary<string, FunctionSignature> BuildSqlServer()
    {
        return new Dictionary<string, FunctionSignature>(StringComparer.OrdinalIgnoreCase)
        {
            ["GETDATE"] = new FunctionSignature("GETDATE", [], "datetime"),
            ["DATEADD"] = new FunctionSignature(
                "DATEADD",
                [
                    new FunctionParameterSignature("datepart", "text"),
                    new FunctionParameterSignature("number", "int"),
                    new FunctionParameterSignature("date", "datetime"),
                ],
                "datetime"),
            ["ISNULL"] = new FunctionSignature(
                "ISNULL",
                [
                    new FunctionParameterSignature("check_expression", "any"),
                    new FunctionParameterSignature("replacement_value", "any"),
                ],
                "any"),
            ["STRING_AGG"] = new FunctionSignature(
                "STRING_AGG",
                [
                    new FunctionParameterSignature("expression", "text"),
                    new FunctionParameterSignature("separator", "text"),
                ],
                "text"),
            ["JSON_VALUE"] = new FunctionSignature(
                "JSON_VALUE",
                [
                    new FunctionParameterSignature("expression", "nvarchar"),
                    new FunctionParameterSignature("path", "nvarchar"),
                ],
                "nvarchar"),
        };
    }

    private static IReadOnlyDictionary<string, FunctionSignature> BuildSQLite()
    {
        return new Dictionary<string, FunctionSignature>(StringComparer.OrdinalIgnoreCase)
        {
            ["datetime"] = new FunctionSignature(
                "datetime",
                [new FunctionParameterSignature("time-value", "text")],
                "text"),
            ["strftime"] = new FunctionSignature(
                "strftime",
                [
                    new FunctionParameterSignature("format", "text"),
                    new FunctionParameterSignature("time-value", "text"),
                ],
                "text"),
            ["ifnull"] = new FunctionSignature(
                "ifnull",
                [
                    new FunctionParameterSignature("value", "any"),
                    new FunctionParameterSignature("fallback", "any"),
                ],
                "any"),
            ["group_concat"] = new FunctionSignature(
                "group_concat",
                [
                    new FunctionParameterSignature("value", "text"),
                    new FunctionParameterSignature("separator", "text"),
                ],
                "text"),
            ["json_extract"] = new FunctionSignature(
                "json_extract",
                [
                    new FunctionParameterSignature("json", "text"),
                    new FunctionParameterSignature("path", "text"),
                ],
                "text"),
        };
    }
}
