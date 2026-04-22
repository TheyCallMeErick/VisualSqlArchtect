using System.Text.RegularExpressions;
using AkkornStudio.UI.Services.Localization;
using AkkornStudio.UI.ViewModels;

namespace AkkornStudio.UI.Services.SqlEditor;

public sealed class MutationGuardService
{
    private readonly ILocalizationService _localization;

    public MutationGuardService(ILocalizationService? localization = null)
    {
        _localization = localization ?? LocalizationService.Instance;
    }

    public MutationGuardResult Analyze(string? sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
            return MutationGuardResult.Safe();

        string statement = sql.Trim();
        string upper = statement.ToUpperInvariant();

        if (upper.StartsWith("DELETE ", StringComparison.Ordinal))
            return AnalyzeDelete(statement, upper, _localization);

        if (upper.StartsWith("UPDATE ", StringComparison.Ordinal))
            return AnalyzeUpdate(statement, upper, _localization);

        if (upper.StartsWith("INSERT ", StringComparison.Ordinal))
            return AnalyzeInsert(statement, upper, _localization);

        if (upper.StartsWith("MERGE ", StringComparison.Ordinal))
            return AnalyzeMerge(statement, _localization);

        if (upper.StartsWith("TRUNCATE ", StringComparison.Ordinal))
            return AnalyzeTruncate(statement, _localization);

        if (IsDdlMutation(upper))
            return AnalyzeDdl(_localization);

        return MutationGuardResult.Safe();
    }

    private static MutationGuardResult AnalyzeDelete(string statement, string upper, ILocalizationService localization)
    {
        var issues = new List<MutationGuardIssue>();
        string? whereClause = ExtractWhereClause(statement, upper);
        if (whereClause is null)
        {
            issues.Add(new MutationGuardIssue(
                MutationGuardSeverity.Critical,
                "NO_WHERE",
                L(localization, "sqlEditor.guard.delete.noWhere.message", "DELETE without WHERE can remove all rows."),
                L(localization, "sqlEditor.guard.delete.noWhere.recommendation", "Add a restrictive WHERE clause before executing.")));
        }
        else if (IsTrivialWhere(whereClause))
        {
            issues.Add(new MutationGuardIssue(
                MutationGuardSeverity.Critical,
                "TRIVIAL_WHERE",
                L(localization, "sqlEditor.guard.delete.trivialWhere.message", "DELETE has a trivially true WHERE clause."),
                L(localization, "sqlEditor.guard.delete.trivialWhere.recommendation", "Use a selective filter to target only intended rows.")));
        }

        bool requiresConfirmation = issues.Any(i => i.Severity == MutationGuardSeverity.Critical);
        return new MutationGuardResult
        {
            IsSafe = !requiresConfirmation,
            RequiresConfirmation = requiresConfirmation,
            Issues = issues,
            CountQuery = BuildCountQuery(statement, upper),
            SupportsDiff = true,
        };
    }

    private static MutationGuardResult AnalyzeUpdate(string statement, string upper, ILocalizationService localization)
    {
        var issues = new List<MutationGuardIssue>();
        string? whereClause = ExtractWhereClause(statement, upper);
        if (whereClause is null)
        {
            issues.Add(new MutationGuardIssue(
                MutationGuardSeverity.Critical,
                "NO_WHERE",
                L(localization, "sqlEditor.guard.update.noWhere.message", "UPDATE without WHERE can affect all rows."),
                L(localization, "sqlEditor.guard.update.noWhere.recommendation", "Add a restrictive WHERE clause before executing.")));
        }
        else if (IsTrivialWhere(whereClause))
        {
            issues.Add(new MutationGuardIssue(
                MutationGuardSeverity.Critical,
                "TRIVIAL_WHERE",
                L(localization, "sqlEditor.guard.update.trivialWhere.message", "UPDATE has a trivially true WHERE clause."),
                L(localization, "sqlEditor.guard.update.trivialWhere.recommendation", "Use a selective filter to target only intended rows.")));
        }

        bool requiresConfirmation = issues.Any(i => i.Severity == MutationGuardSeverity.Critical);
        return new MutationGuardResult
        {
            IsSafe = !requiresConfirmation,
            RequiresConfirmation = requiresConfirmation,
            Issues = issues,
            CountQuery = BuildCountQuery(statement, upper),
            SupportsDiff = true,
        };
    }

    private static MutationGuardResult AnalyzeInsert(string statement, string upper, ILocalizationService localization)
    {
        bool hasColumnList = Regex.IsMatch(
            statement,
            @"^\s*INSERT\s+INTO\s+[^\s(]+\s*\(",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (hasColumnList)
            return MutationGuardResult.Safe();

        return new MutationGuardResult
        {
            IsSafe = true,
            RequiresConfirmation = false,
            Issues =
            [
                new MutationGuardIssue(
                    MutationGuardSeverity.Info,
                    "INSERT_WITHOUT_COLUMN_LIST",
                    L(localization, "sqlEditor.guard.insert.noColumnList.message", "INSERT without explicit column list is fragile against schema changes."),
                    L(localization, "sqlEditor.guard.insert.noColumnList.recommendation", "Prefer INSERT INTO table(col1, col2, ...) VALUES (...).")),
            ],
            CountQuery = null,
            SupportsDiff = true,
        };
    }

    private static MutationGuardResult AnalyzeMerge(string statement, ILocalizationService localization)
    {
        return new MutationGuardResult
        {
            IsSafe = false,
            RequiresConfirmation = true,
            Issues =
            [
                new MutationGuardIssue(
                    MutationGuardSeverity.Critical,
                    "MERGE_MUTATION",
                    L(localization, "sqlEditor.guard.merge.message", "MERGE can update, insert, or delete rows in the target table."),
                    L(localization, "sqlEditor.guard.merge.recommendation", "Confirm execution only after reviewing the target, source, and match condition.")),
            ],
            CountQuery = BuildMergeCountQuery(statement),
            SupportsDiff = true,
        };
    }

    private static MutationGuardResult AnalyzeTruncate(string statement, ILocalizationService localization)
    {
        return new MutationGuardResult
        {
            IsSafe = false,
            RequiresConfirmation = true,
            Issues =
            [
                new MutationGuardIssue(
                    MutationGuardSeverity.Critical,
                    "TRUNCATE_MUTATION",
                    L(localization, "sqlEditor.guard.truncate.message", "TRUNCATE removes all rows from the target table."),
                    L(localization, "sqlEditor.guard.truncate.recommendation", "Confirm execution only when a full table reset is intended.")),
            ],
            CountQuery = BuildTruncateCountQuery(statement),
            SupportsDiff = true,
        };
    }

    private static MutationGuardResult AnalyzeDdl(ILocalizationService localization)
    {
        return new MutationGuardResult
        {
            IsSafe = false,
            RequiresConfirmation = true,
            Issues =
            [
                new MutationGuardIssue(
                    MutationGuardSeverity.Critical,
                    "DDL_MUTATION",
                    L(localization, "sqlEditor.guard.ddl.message", "DDL statement may cause structural changes in the database."),
                    L(localization, "sqlEditor.guard.ddl.recommendation", "Confirm execution only when schema changes are intended.")),
            ],
            CountQuery = null,
            SupportsDiff = false,
        };
    }

    private static bool IsDdlMutation(string upper) =>
        upper.StartsWith("ALTER ", StringComparison.Ordinal) ||
        upper.StartsWith("DROP ", StringComparison.Ordinal) ||
        upper.StartsWith("CREATE ", StringComparison.Ordinal);

    private static string? ExtractWhereClause(string statement, string upper)
    {
        int whereIndex = upper.IndexOf(" WHERE ", StringComparison.Ordinal);
        if (whereIndex < 0)
            return null;

        return statement[(whereIndex + " WHERE ".Length)..].Trim().TrimEnd(';');
    }

    private static bool IsTrivialWhere(string whereClause)
    {
        string normalized = Regex.Replace(whereClause, @"\s+", string.Empty).ToUpperInvariant();
        return normalized is "1=1" or "TRUE" or "(1=1)" or "(TRUE)";
    }

    private static string? BuildCountQuery(string statement, string upper)
    {
        if (upper.StartsWith("DELETE ", StringComparison.Ordinal))
        {
            Match m = Regex.Match(
                statement,
                @"^\s*DELETE\s+FROM\s+([^\s;]+)\s*(?:WHERE\s+(.+?))?\s*;?\s*$",
                RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant);
            if (!m.Success)
                return null;

            string table = m.Groups[1].Value.Trim();
            string where = m.Groups[2].Value.Trim().TrimEnd(';');
            return string.IsNullOrWhiteSpace(where)
                ? $"SELECT COUNT(*) FROM {table}"
                : $"SELECT COUNT(*) FROM {table} WHERE {where}";
        }

        if (upper.StartsWith("UPDATE ", StringComparison.Ordinal))
        {
            Match m = Regex.Match(
                statement,
                @"^\s*UPDATE\s+([^\s;]+)\s+SET\s+.+?(?:\s+WHERE\s+(.+?))?\s*;?\s*$",
                RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant);
            if (!m.Success)
                return null;

            string table = m.Groups[1].Value.Trim();
            string where = m.Groups[2].Value.Trim().TrimEnd(';');
            return string.IsNullOrWhiteSpace(where)
                ? $"SELECT COUNT(*) FROM {table}"
                : $"SELECT COUNT(*) FROM {table} WHERE {where}";
        }

        return null;
    }

    private static string? BuildTruncateCountQuery(string statement)
    {
        Match m = Regex.Match(
            statement,
            @"^\s*TRUNCATE\s+(?:TABLE\s+)?([^\s,;]+)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (!m.Success)
            return null;

        string table = m.Groups[1].Value.Trim();
        return $"SELECT COUNT(*) FROM {table}";
    }

    private static string? BuildMergeCountQuery(string statement)
    {
        Match m = Regex.Match(
            statement,
            @"^\s*MERGE\s+INTO\s+([^\s;]+)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (!m.Success)
            return null;

        string table = m.Groups[1].Value.Trim();
        return $"SELECT COUNT(*) FROM {table}";
    }

    private static string L(ILocalizationService localization, string key, string fallback)
    {
        string value = localization[key];
        return string.Equals(value, key, StringComparison.Ordinal) ? fallback : value;
    }
}
