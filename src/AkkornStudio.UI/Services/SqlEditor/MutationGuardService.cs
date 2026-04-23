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

        if (TrySplitLeadingWithMutation(statement, out string ctePrefix, out string mutationSql))
            return PrefixCountQuery(Analyze(mutationSql), ctePrefix);

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

    private static MutationGuardResult PrefixCountQuery(MutationGuardResult result, string ctePrefix)
    {
        if (string.IsNullOrWhiteSpace(result.CountQuery))
            return result;

        return new MutationGuardResult
        {
            IsSafe = result.IsSafe,
            RequiresConfirmation = result.RequiresConfirmation,
            Issues = result.Issues,
            CountQuery = $"{ctePrefix} {result.CountQuery}",
            SupportsDiff = result.SupportsDiff,
        };
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
            Match usingMatch = Regex.Match(
                statement,
                @"^\s*DELETE\s+FROM\s+(?<target>[^\s;]+)(?:\s+(?:AS\s+)?(?<alias>[A-Za-z_][A-Za-z0-9_]*))?\s+USING\s+(?<using>.+?)(?:\s+WHERE\s+(?<where>.+?))?\s*;?\s*$",
                RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant);
            if (usingMatch.Success)
            {
                string target = usingMatch.Groups["target"].Value.Trim();
                string alias = usingMatch.Groups["alias"].Success ? usingMatch.Groups["alias"].Value.Trim() : string.Empty;
                string usingClause = usingMatch.Groups["using"].Value.Trim().TrimEnd(';');
                string usingWhere = usingMatch.Groups["where"].Value.Trim().TrimEnd(';');
                string targetClause = string.IsNullOrWhiteSpace(alias) ? target : $"{target} {alias}";
                return string.IsNullOrWhiteSpace(usingWhere)
                    ? $"SELECT COUNT(*) FROM {targetClause}, {usingClause}"
                    : $"SELECT COUNT(*) FROM {targetClause}, {usingClause} WHERE {usingWhere}";
            }

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
            Match fromMatch = Regex.Match(
                statement,
                @"^\s*UPDATE\s+(?<target>[^\s;]+)(?:\s+(?:AS\s+)?(?<alias>[A-Za-z_][A-Za-z0-9_]*))?\s+SET\s+.+?\s+FROM\s+(?<from>.+?)(?:\s+WHERE\s+(?<where>.+?))?\s*;?\s*$",
                RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant);
            if (fromMatch.Success)
            {
                string target = fromMatch.Groups["target"].Value.Trim();
                string alias = fromMatch.Groups["alias"].Success ? fromMatch.Groups["alias"].Value.Trim() : string.Empty;
                string fromClause = fromMatch.Groups["from"].Value.Trim().TrimEnd(';');
                string fromWhere = fromMatch.Groups["where"].Value.Trim().TrimEnd(';');
                string targetClause = string.IsNullOrWhiteSpace(alias) ? target : $"{target} {alias}";
                return string.IsNullOrWhiteSpace(fromWhere)
                    ? $"SELECT COUNT(*) FROM {targetClause}, {fromClause}"
                    : $"SELECT COUNT(*) FROM {targetClause}, {fromClause} WHERE {fromWhere}";
            }

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

    private static bool TrySplitLeadingWithMutation(string statement, out string ctePrefix, out string mutationSql)
    {
        ctePrefix = string.Empty;
        mutationSql = statement;

        int index = 0;
        while (index < statement.Length && char.IsWhiteSpace(statement[index]))
            index++;

        if (!statement.AsSpan(index).StartsWith("WITH", StringComparison.OrdinalIgnoreCase))
            return false;

        index += 4;
        bool consumedDefinition = false;
        while (index < statement.Length)
        {
            while (index < statement.Length && (char.IsWhiteSpace(statement[index]) || statement[index] == ','))
                index++;

            if (index >= statement.Length || !IsIdentifierStart(statement[index]))
                return false;

            index++;
            while (index < statement.Length && IsIdentifierPart(statement[index]))
                index++;

            while (index < statement.Length && char.IsWhiteSpace(statement[index]))
                index++;

            if (index < statement.Length && statement[index] == '(')
            {
                if (!TrySkipBalancedParentheses(statement, ref index))
                    return false;

                while (index < statement.Length && char.IsWhiteSpace(statement[index]))
                    index++;
            }

            if (!statement.AsSpan(index).StartsWith("AS", StringComparison.OrdinalIgnoreCase))
                return false;

            index += 2;
            while (index < statement.Length && char.IsWhiteSpace(statement[index]))
                index++;

            if (index >= statement.Length || statement[index] != '(')
                return false;

            if (!TrySkipBalancedParentheses(statement, ref index))
                return false;

            consumedDefinition = true;
            while (index < statement.Length && char.IsWhiteSpace(statement[index]))
                index++;

            if (index < statement.Length && statement[index] == ',')
            {
                index++;
                continue;
            }

            break;
        }

        if (!consumedDefinition || index >= statement.Length)
            return false;

        string remainder = statement[index..].TrimStart();
        string upperRemainder = remainder.ToUpperInvariant();
        if (!upperRemainder.StartsWith("UPDATE ", StringComparison.Ordinal)
            && !upperRemainder.StartsWith("DELETE ", StringComparison.Ordinal)
            && !upperRemainder.StartsWith("INSERT ", StringComparison.Ordinal)
            && !upperRemainder.StartsWith("MERGE ", StringComparison.Ordinal))
        {
            return false;
        }

        ctePrefix = statement[..index].Trim();
        mutationSql = remainder;
        return true;
    }

    private static bool TrySkipBalancedParentheses(string value, ref int index)
    {
        if (index >= value.Length || value[index] != '(')
            return false;

        int depth = 0;
        bool inSingleQuote = false;
        while (index < value.Length)
        {
            char current = value[index];
            if (current == '\'' && (index == 0 || value[index - 1] != '\\'))
                inSingleQuote = !inSingleQuote;

            if (!inSingleQuote)
            {
                if (current == '(')
                    depth++;
                else if (current == ')')
                {
                    depth--;
                    if (depth == 0)
                    {
                        index++;
                        return true;
                    }
                }
            }

            index++;
        }

        return false;
    }

    private static bool IsIdentifierStart(char c) => char.IsLetter(c) || c == '_';

    private static bool IsIdentifierPart(char c) => char.IsLetterOrDigit(c) || c == '_';

    private static string L(ILocalizationService localization, string key, string fallback)
    {
        string value = localization[key];
        return string.Equals(value, key, StringComparison.Ordinal) ? fallback : value;
    }
}
