using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using VisualSqlArchitect.UI.Services.SqlImport.Rewriting;
using VisualSqlArchitect.UI.ViewModels.Canvas;

namespace VisualSqlArchitect.UI.Services.SqlImport.Execution.Parsing;

public sealed class SqlImportClauseParser(SqlImportCteRewriteService cteRewriteService)
{
    private readonly SqlImportCteRewriteService _cteRewriteService = cteRewriteService;

    public SqlImportParseResult Parse(
        string sql,
        ObservableCollection<ImportReportItem> report,
        CancellationToken cancellationToken
    )
    {
        int imported = 0;
        int partial = 0;
        int skipped = 0;

        sql = Regex.Replace(sql, @"--[^\n]*", " ");
        sql = Regex.Replace(sql, @"/\*.*?\*/", " ", RegexOptions.Singleline);
        sql = Regex.Replace(sql, @"\s+", " ").Trim();

        if (Regex.IsMatch(sql, @"\bWITH\b", RegexOptions.IgnoreCase)
            && _cteRewriteService.TryRewriteSimpleCteQuery(sql, out string rewrittenSql, out int rewrittenCteCount))
        {
            sql = rewrittenSql;
            report.Add(
                new ImportReportItem(
                    "CTE",
                    EImportItemStatus.Imported,
                    rewrittenCteCount == 1
                        ? "Single CTE rewritten to supported import shape."
                        : $"{rewrittenCteCount} chained CTEs rewritten to supported import shape."
                )
            );
            imported++;
        }

        bool hasSupportedWhereSubquery = Regex.IsMatch(
            sql,
            @"\bWHERE\s+(?:EXISTS\s*\(\s*SELECT|\w+(?:\.\w+)?\s+(?:NOT\s+)?IN\s*\(\s*SELECT|\w+(?:\.\w+)?\s*(?:=|<>|!=|>|>=|<|<=)\s*\(\s*SELECT)",
            RegexOptions.IgnoreCase
        );

        bool hasUnsupportedCteOrSubquery =
            Regex.IsMatch(sql, @"\bWITH\b", RegexOptions.IgnoreCase)
            || (Regex.IsMatch(sql, @"\(SELECT\b", RegexOptions.IgnoreCase) && !hasSupportedWhereSubquery);

        bool hasCorrelatedSubquery =
            Regex.IsMatch(sql, @"\b(EXISTS|IN)\s*\(\s*SELECT\b", RegexOptions.IgnoreCase)
            && Regex.IsMatch(sql, @"\b\w+\.\w+\s*=\s*\w+\.\w+\b", RegexOptions.IgnoreCase);

        Match fromMatchForAlias = Regex.Match(
            sql,
            @"FROM\s+(.+?)(?=\s+(?:WHERE|ORDER\s+BY|LIMIT|GROUP\s+BY)|$)",
            RegexOptions.IgnoreCase | RegexOptions.Singleline
        );

        HashSet<string> outerAliases = _cteRewriteService.ExtractSourceAliases(
            fromMatchForAlias.Success ? fromMatchForAlias.Groups[1].Value.Trim() : string.Empty
        );

        if (hasUnsupportedCteOrSubquery)
        {
            if (Regex.IsMatch(sql, @"\bWITH\b", RegexOptions.IgnoreCase))
            {
                foreach (string cteIssue in _cteRewriteService.AnalyzeCteNameIssues(sql))
                {
                    report.Add(new ImportReportItem("CTE name diagnostics", EImportItemStatus.Partial, cteIssue));
                    partial++;
                }
            }

            if (hasCorrelatedSubquery)
            {
                string correlatedFields = SqlImportCteRewriteService.DescribeCorrelatedOuterReferences(sql, outerAliases);
                report.Add(
                    new ImportReportItem(
                        "Correlated sub-query",
                        EImportItemStatus.Partial,
                        string.IsNullOrWhiteSpace(correlatedFields)
                            ? "Correlated sub-query is not yet supported and falls back to a safe partial import path."
                            : $"Correlated sub-query is not yet supported and falls back to a safe partial import path. External refs: {correlatedFields}."
                    )
                );
                partial++;
            }

            report.Add(new ImportReportItem("CTE / sub-query", EImportItemStatus.Skipped, "CTEs and sub-queries are not supported"));
            skipped++;

            report.Add(
                new ImportReportItem(
                    "Raw fallback",
                    EImportItemStatus.Skipped,
                    "Raw fallback is disabled for CTE/sub-query blocks to avoid unsafe or ambiguous SQL materialization."
                )
            );
            skipped++;

            return new SqlImportParseResult(null, imported, partial, skipped, true);
        }

        if (Regex.IsMatch(sql, @"\bUNION\b", RegexOptions.IgnoreCase))
        {
            report.Add(new ImportReportItem("UNION", EImportItemStatus.Skipped, "UNION is not supported"));
            skipped++;
        }

        Match selMatch = Regex.Match(
            sql,
            @"SELECT\s+(DISTINCT\s+)?(.+?)\s+FROM\b",
            RegexOptions.IgnoreCase | RegexOptions.Singleline
        );
        if (!selMatch.Success)
            throw new InvalidOperationException("Could not find SELECT ... FROM in the query.");

        bool isDistinct = selMatch.Groups[1].Success;
        string colPart = selMatch.Groups[2].Value.Trim();
        bool isStar = colPart == "*";

        var selectedCols = new List<SqlImportSelectedColumn>();
        if (!isStar)
        {
            foreach (string raw in SplitCommas(colPart))
            {
                cancellationToken.ThrowIfCancellationRequested();
                string col = raw.Trim();
                Match asMatch = Regex.Match(col, @"^(.+?)\s+AS\s+(\w+)$", RegexOptions.IgnoreCase);
                if (asMatch.Success)
                    selectedCols.Add(new SqlImportSelectedColumn(asMatch.Groups[1].Value.Trim(), asMatch.Groups[2].Value));
                else
                    selectedCols.Add(new SqlImportSelectedColumn(col, null));
            }
        }

        Match fromBlock = Regex.Match(
            sql,
            @"FROM\s+(.+?)(?=\s+(?:WHERE|ORDER\s+BY|LIMIT|GROUP\s+BY)|$)",
            RegexOptions.IgnoreCase | RegexOptions.Singleline
        );
        if (!fromBlock.Success)
            throw new InvalidOperationException("Could not parse FROM clause.");

        string fromSql = fromBlock.Groups[1].Value.Trim();

        string[] joinKeywords = ["INNER JOIN", "LEFT JOIN", "RIGHT JOIN", "FULL JOIN", "JOIN"];
        var fromParts = new List<SqlImportSourcePart>();

        int firstJoinIdx = -1;
        string upperFrom = fromSql.ToUpperInvariant();
        foreach (string joinKeyword in joinKeywords)
        {
            int idx = upperFrom.IndexOf(joinKeyword, StringComparison.Ordinal);
            if (idx >= 0 && (firstJoinIdx < 0 || idx < firstJoinIdx))
                firstJoinIdx = idx;
        }

        string primaryPart = firstJoinIdx >= 0 ? fromSql[..firstJoinIdx].Trim() : fromSql.Trim();
        string primaryTable = ExtractTableName(primaryPart);
        fromParts.Add(new SqlImportSourcePart(primaryTable, null, null));

        MatchCollection joinMatches = Regex.Matches(
            fromSql,
            @"(?:INNER\s+JOIN|LEFT\s+(?:OUTER\s+)?JOIN|RIGHT\s+(?:OUTER\s+)?JOIN|FULL\s+(?:OUTER\s+)?JOIN|JOIN)\s+(\w+(?:\.\w+)?)(?:\s+(?:AS\s+)?\w+)?\s+ON\s+(.+?)(?=\s+(?:INNER\s+JOIN|LEFT\s+JOIN|RIGHT\s+JOIN|FULL\s+JOIN|JOIN)|$)",
            RegexOptions.IgnoreCase | RegexOptions.Singleline
        );
        foreach (Match jm in joinMatches)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string jTable = jm.Groups[1].Value.Trim();
            string onClause = jm.Groups[2].Value.Trim();
            string jType = Regex.Match(
                    jm.Value,
                    @"(?:INNER\s+JOIN|LEFT\s+(?:OUTER\s+)?JOIN|RIGHT\s+(?:OUTER\s+)?JOIN|FULL\s+(?:OUTER\s+)?JOIN|JOIN)",
                    RegexOptions.IgnoreCase
                )
                .Value.Trim()
                .ToUpperInvariant();
            fromParts.Add(new SqlImportSourcePart(jTable, jType, onClause));
        }

        Match whereMatch = Regex.Match(
            sql,
            @"WHERE\s+(.+?)(?=\s+(?:ORDER\s+BY|LIMIT|TOP|GROUP\s+BY)|$)",
            RegexOptions.IgnoreCase | RegexOptions.Singleline
        );
        string? whereClause = whereMatch.Success ? whereMatch.Groups[1].Value.Trim() : null;

        Match orderMatch = Regex.Match(
            sql,
            @"ORDER\s+BY\s+(.+?)(?=\s+LIMIT|$)",
            RegexOptions.IgnoreCase | RegexOptions.Singleline
        );
        string? orderBy = orderMatch.Success ? orderMatch.Groups[1].Value.Trim() : null;

        Match groupMatch = Regex.Match(
            sql,
            @"GROUP\s+BY\s+(.+?)(?=\s+(?:HAVING|ORDER\s+BY|LIMIT|TOP)|$)",
            RegexOptions.IgnoreCase | RegexOptions.Singleline
        );
        string? groupBy = groupMatch.Success ? groupMatch.Groups[1].Value.Trim() : null;

        Match havingMatch = Regex.Match(
            sql,
            @"HAVING\s+(.+?)(?=\s+(?:ORDER\s+BY|LIMIT|TOP)|$)",
            RegexOptions.IgnoreCase | RegexOptions.Singleline
        );
        string? havingClause = havingMatch.Success ? havingMatch.Groups[1].Value.Trim() : null;

        int? limitVal = null;
        Match limitMatch = Regex.Match(sql, @"\bLIMIT\s+(\d+)", RegexOptions.IgnoreCase);
        if (limitMatch.Success)
            limitVal = int.Parse(limitMatch.Groups[1].Value);
        else
        {
            Match topMatch = Regex.Match(sql, @"\bTOP\s+(\d+)", RegexOptions.IgnoreCase);
            if (topMatch.Success)
                limitVal = int.Parse(topMatch.Groups[1].Value);
        }

        var parsed = new SqlImportParsedQuery(
            isDistinct,
            isStar,
            selectedCols,
            fromParts,
            whereClause,
            orderBy,
            groupBy,
            havingClause,
            limitVal,
            outerAliases
        );

        return new SqlImportParseResult(parsed, imported, partial, skipped, false);
    }

    private static string ExtractTableName(string part)
    {
        string trimmed = part.Trim();
        Match match = Regex.Match(trimmed, @"^([\w.]+)(?:\s+(?:AS\s+)?\w+)?$", RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value : trimmed.Split(' ')[0];
    }

    private static List<string> SplitCommas(string value)
    {
        var parts = new List<string>();
        int depth = 0;
        int start = 0;
        for (int i = 0; i < value.Length; i++)
        {
            if (value[i] == '(')
                depth++;
            else if (value[i] == ')')
                depth--;
            else if (value[i] == ',' && depth == 0)
            {
                parts.Add(value[start..i]);
                start = i + 1;
            }
        }

        parts.Add(value[start..]);
        return parts;
    }
}
