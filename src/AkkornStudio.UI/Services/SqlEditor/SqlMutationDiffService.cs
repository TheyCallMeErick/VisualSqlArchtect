using System.Text.RegularExpressions;
using AkkornStudio.Core;
using AkkornStudio.UI.Services.Localization;
using AkkornStudio.UI.ViewModels;

namespace AkkornStudio.UI.Services.SqlEditor;

public sealed class SqlMutationDiffService
{
    private readonly Func<string, ConnectionConfig?, int, CancellationToken, Task<SqlEditorResultSet>> _executeAsync;
    private readonly ILocalizationService _localization;

    public SqlMutationDiffService(
        SqlEditorExecutionService executionService,
        ILocalizationService? localization = null)
    {
        ArgumentNullException.ThrowIfNull(executionService);
        _executeAsync = executionService.ExecuteAsync;
        _localization = localization ?? LocalizationService.Instance;
    }

    internal SqlMutationDiffService(
        Func<string, ConnectionConfig?, int, CancellationToken, Task<SqlEditorResultSet>> executeAsync,
        ILocalizationService? localization = null)
    {
        _executeAsync = executeAsync ?? throw new ArgumentNullException(nameof(executeAsync));
        _localization = localization ?? LocalizationService.Instance;
    }

    public async Task<SqlMutationDiffPreview> BuildPreviewAsync(
        string statementSql,
        MutationGuardResult guard,
        ConnectionConfig? config,
        long? estimatedAffectedRows,
        CancellationToken ct = default)
    {
        if (!guard.SupportsDiff)
            return SqlMutationDiffPreview.Unavailable(L("sqlEditor.diff.unavailable.noPreview", "No transactional diff preview available for this statement."));

        string upper = statementSql.TrimStart().ToUpperInvariant();

        if (upper.StartsWith("INSERT ", StringComparison.Ordinal))
            return await BuildInsertPreviewAsync(statementSql, config, ct);

        if (string.IsNullOrWhiteSpace(guard.CountQuery))
            return SqlMutationDiffPreview.Unavailable(L("sqlEditor.diff.unavailable.noPreview", "No transactional diff preview available for this statement."));

        ParsedCountQuery? parsed = ParseCountQuery(guard.CountQuery);
        if (parsed is null)
            return SqlMutationDiffPreview.Unavailable(L("sqlEditor.diff.unavailable.parseError", "Could not parse mutation target for transactional diff preview."));

        long? affectedRows = estimatedAffectedRows ?? await ExecuteScalarCountAsync(guard.CountQuery, config, ct);
        long? totalBefore = await ExecuteScalarCountAsync($"SELECT COUNT(*) FROM {parsed.TableName}", config, ct);
        if (!affectedRows.HasValue || !totalBefore.HasValue)
            return SqlMutationDiffPreview.Unavailable(L("sqlEditor.diff.unavailable.connection", "Transactional diff preview unavailable due to connection or query limitations."));

        if (upper.StartsWith("DELETE ", StringComparison.Ordinal))
        {
            long totalAfter = Math.Max(0, totalBefore.Value - affectedRows.Value);
            return new SqlMutationDiffPreview
            {
                Available = true,
                Message = string.Format(
                    L(
                        "sqlEditor.diff.deleteSummary",
                        "Transactional diff preview (ROLLBACK guaranteed): table {0}, total rows before {1}, affected {2}, total rows after {3}."),
                    parsed.TableName,
                    totalBefore.Value,
                    affectedRows.Value,
                    totalAfter),
            };
        }

        if (upper.StartsWith("UPDATE ", StringComparison.Ordinal))
        {
            return new SqlMutationDiffPreview
            {
                Available = true,
                Message = string.Format(
                    L(
                        "sqlEditor.diff.updateSummary",
                        "Transactional diff preview (ROLLBACK guaranteed): table {0}, total rows before {1}, candidate rows affected {2}, total rows after {3}."),
                    parsed.TableName,
                    totalBefore.Value,
                    affectedRows.Value,
                    totalBefore.Value),
            };
        }

        return SqlMutationDiffPreview.Unavailable(L("sqlEditor.diff.unavailable.unsupportedStatement", "Transactional diff preview currently supports UPDATE and DELETE only."));
    }

    private async Task<SqlMutationDiffPreview> BuildInsertPreviewAsync(
        string statementSql,
        ConnectionConfig? config,
        CancellationToken ct)
    {
        string? targetTable = ParseInsertTargetTable(statementSql);
        if (string.IsNullOrWhiteSpace(targetTable))
            return SqlMutationDiffPreview.Unavailable(L("sqlEditor.diff.unavailable.parseError", "Could not parse mutation target for transactional diff preview."));

        long? totalBefore = await ExecuteScalarCountAsync($"SELECT COUNT(*) FROM {targetTable}", config, ct);
        if (!totalBefore.HasValue)
            return SqlMutationDiffPreview.Unavailable(L("sqlEditor.diff.unavailable.connection", "Transactional diff preview unavailable due to connection or query limitations."));

        long? insertedRows = TryEstimateInsertedRows(statementSql);
        if (insertedRows.HasValue)
        {
            return new SqlMutationDiffPreview
            {
                Available = true,
                Message = string.Format(
                    L(
                        "sqlEditor.diff.insertSummary",
                        "Transactional diff preview (ROLLBACK guaranteed): table {0}, total rows before {1}, estimated inserted rows {2}, total rows after {3}."),
                    targetTable,
                    totalBefore.Value,
                    insertedRows.Value,
                    totalBefore.Value + insertedRows.Value),
            };
        }

        return new SqlMutationDiffPreview
        {
            Available = true,
            Message = string.Format(
                L(
                    "sqlEditor.diff.insertSummaryUnknown",
                    "Transactional diff preview (ROLLBACK guaranteed): table {0}, total rows before {1}. Inserted row count could not be estimated from this INSERT shape."),
                targetTable,
                totalBefore.Value),
        };
    }

    private async Task<long?> ExecuteScalarCountAsync(string sql, ConnectionConfig? config, CancellationToken ct)
    {
        SqlEditorResultSet result = await _executeAsync(sql, config, 1, ct);
        if (!result.Success || result.Data is null || result.Data.Rows.Count == 0 || result.Data.Columns.Count == 0)
            return null;

        object? value = result.Data.Rows[0][0];
        if (value is null || value is DBNull)
            return null;

        return long.TryParse(value.ToString(), out long parsed) ? parsed : null;
    }

    private static ParsedCountQuery? ParseCountQuery(string countQuery)
    {
        Match match = Regex.Match(
            countQuery,
            @"^\s*SELECT\s+COUNT\(\*\)\s+FROM\s+([^\s;]+)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (!match.Success)
            return null;

        return new ParsedCountQuery(match.Groups[1].Value.Trim());
    }

    private sealed record ParsedCountQuery(string TableName);

    private static string? ParseInsertTargetTable(string statementSql)
    {
        Match match = Regex.Match(
            statementSql,
            @"^\s*INSERT\s+INTO\s+([^\s(]+)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        return match.Success ? match.Groups[1].Value.Trim() : null;
    }

    private static long? TryEstimateInsertedRows(string statementSql)
    {
        Match valuesMatch = Regex.Match(
            statementSql,
            @"\bVALUES\b(?<values>.+)$",
            RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant);
        if (!valuesMatch.Success)
            return null;

        string valuesSegment = valuesMatch.Groups["values"].Value;
        int tupleCount = Regex.Matches(valuesSegment, @"\(", RegexOptions.CultureInvariant).Count;
        return tupleCount > 0 ? tupleCount : null;
    }

    private string L(string key, string fallback)
    {
        string value = _localization[key];
        return string.Equals(value, key, StringComparison.Ordinal) ? fallback : value;
    }
}
