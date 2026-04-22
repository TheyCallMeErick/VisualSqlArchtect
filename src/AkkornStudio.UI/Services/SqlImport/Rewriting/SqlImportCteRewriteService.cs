using System.Text.RegularExpressions;

namespace AkkornStudio.UI.Services.SqlImport.Rewriting;

public sealed class SqlImportCteRewriteService
{
    private const string SimpleSubqueryForbiddenKeywordsPattern =
        @"\b(WHERE|GROUP\s+BY|HAVING|ORDER\s+BY|JOIN|UNION|LIMIT|OFFSET|TOP|DISTINCT)\b";

    public IEnumerable<string> AnalyzeCteNameIssues(string sql)
    {
        var issues = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        MatchCollection cteMatches = Regex.Matches(
            sql,
            @"(?:\bWITH\b|,)\s*([^\s,()]+)\s*(?:\([^)]*\))?\s+AS\s*\(",
            RegexOptions.IgnoreCase
        );

        foreach (Match m in cteMatches)
        {
            string rawName = m.Groups[1].Value.Trim();
            if (!Regex.IsMatch(rawName, @"^[A-Za-z_][A-Za-z0-9_]*$"))
            {
                issues.Add($"Invalid CTE name '{rawName}'. Use letters, digits, and underscore; first char must be a letter or underscore.");
                continue;
            }

            if (!seen.Add(rawName))
            {
                issues.Add($"Duplicate CTE name '{rawName}'. CTE names must be unique in a WITH block.");
            }
        }

        return issues;
    }

    public bool TryRewriteSimpleCteQuery(string sql, out string rewrittenSql, out int cteCount)
    {
        rewrittenSql = sql;
        cteCount = 0;

        if (!Regex.IsMatch(sql, @"^\s*WITH\b", RegexOptions.IgnoreCase))
            return false;

        if (!TryExtractCteDefinitions(sql, out List<(string name, string body)> definitions, out string mainQuery))
            return false;

        if (AnalyzeCteNameIssues(sql).Any())
            return false;

        var resolvedSources = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach ((string name, string body) in definitions)
        {
            if (!TryExtractSimpleSelectSource(body, out string sourceName))
                return false;

            if (resolvedSources.TryGetValue(sourceName, out string? chainedSource))
                resolvedSources[name] = chainedSource;
            else
                resolvedSources[name] = sourceName;
        }

        string rewrittenMain = Regex.Replace(
            mainQuery,
            @"\b(FROM|JOIN)\s+([A-Za-z_][A-Za-z0-9_]*)\b",
            m =>
            {
                string keyword = m.Groups[1].Value;
                string source = m.Groups[2].Value;
                return resolvedSources.TryGetValue(source, out string? resolved)
                    ? $"{keyword} {resolved}"
                    : m.Value;
            },
            RegexOptions.IgnoreCase
        );

        cteCount = definitions.Count;
        rewrittenSql = rewrittenMain;
        return true;
    }

    public bool TryRewriteSimpleFromSubquery(string sql, out string rewrittenSql)
    {
        rewrittenSql = sql;

        Match fromSubqueryMatch = Regex.Match(
            sql,
            @"\bFROM\s*\(\s*(?<subquery>SELECT.+?)\s*\)\s+(?:AS\s+)?(?<alias>[A-Za-z_][A-Za-z0-9_]*)\b",
            RegexOptions.IgnoreCase | RegexOptions.Singleline
        );

        if (!fromSubqueryMatch.Success)
            return false;

        string subquerySql = fromSubqueryMatch.Groups["subquery"].Value.Trim();
        string alias = fromSubqueryMatch.Groups["alias"].Value.Trim();

        if (!TryExtractSimplePassThroughSelectSource(subquerySql, out string sourceName))
            return false;

        rewrittenSql = sql[..fromSubqueryMatch.Index]
            + $"FROM {sourceName} {alias}"
            + sql[(fromSubqueryMatch.Index + fromSubqueryMatch.Length)..];

        return true;
    }

    public HashSet<string> ExtractSourceAliases(string fromSql)
    {
        var aliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(fromSql))
            return aliases;

        void AddMatchAliases(Match m)
        {
            string source = m.Groups[1].Value.Trim();
            string explicitAlias = m.Groups[2].Success ? m.Groups[2].Value.Trim() : string.Empty;
            string implicitAlias = m.Groups[3].Success ? m.Groups[3].Value.Trim() : string.Empty;

            if (!string.IsNullOrWhiteSpace(explicitAlias))
                aliases.Add(explicitAlias);
            if (!string.IsNullOrWhiteSpace(implicitAlias))
                aliases.Add(implicitAlias);

            string shortName = source.Split('.').Last();
            if (!string.IsNullOrWhiteSpace(shortName))
                aliases.Add(shortName);
        }

        Match primary = Regex.Match(
            fromSql,
            @"^([A-Za-z_][A-Za-z0-9_\.]*)\s*(?:AS\s+([A-Za-z_][A-Za-z0-9_]*)|([A-Za-z_][A-Za-z0-9_]*))?",
            RegexOptions.IgnoreCase
        );
        if (primary.Success)
            AddMatchAliases(primary);

        MatchCollection joins = Regex.Matches(
            fromSql,
            @"(?:INNER\s+JOIN|LEFT\s+(?:OUTER\s+)?JOIN|RIGHT\s+(?:OUTER\s+)?JOIN|FULL\s+(?:OUTER\s+)?JOIN|JOIN)\s+([A-Za-z_][A-Za-z0-9_\.]*)\s*(?:AS\s+([A-Za-z_][A-Za-z0-9_]*)|([A-Za-z_][A-Za-z0-9_]*))?",
            RegexOptions.IgnoreCase | RegexOptions.Singleline
        );
        foreach (Match jm in joins)
            AddMatchAliases(jm);

        return aliases;
    }

    public static string DescribeCorrelatedOuterReferences(string sql, HashSet<string> outerAliases)
    {
        if (outerAliases.Count == 0 || string.IsNullOrWhiteSpace(sql))
            return string.Empty;

        var refs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        MatchCollection qualifiedRefs = Regex.Matches(sql, @"\b([A-Za-z_][A-Za-z0-9_]*)\.([A-Za-z_][A-Za-z0-9_]*)\b");
        foreach (Match match in qualifiedRefs)
        {
            string alias = match.Groups[1].Value;
            string col = match.Groups[2].Value;
            if (outerAliases.Contains(alias))
                refs.Add($"{alias}.{col}");
        }

        return string.Join(", ", refs.OrderBy(r => r, StringComparer.OrdinalIgnoreCase));
    }

    private static bool TryExtractCteDefinitions(
        string sql,
        out List<(string name, string body)> definitions,
        out string mainQuery
    )
    {
        definitions = [];
        mainQuery = sql;

        int index = 0;
        while (index < sql.Length && char.IsWhiteSpace(sql[index]))
            index++;

        if (!sql.AsSpan(index).StartsWith("WITH", StringComparison.OrdinalIgnoreCase))
            return false;

        index += 4;
        while (index < sql.Length)
        {
            while (index < sql.Length && (char.IsWhiteSpace(sql[index]) || sql[index] == ','))
                index++;
            if (index >= sql.Length)
                return false;

            int nameStart = index;
            if (!IsIdentifierStart(sql[index]))
                return false;

            index++;
            while (index < sql.Length && IsIdentifierPart(sql[index]))
                index++;

            string name = sql[nameStart..index];

            while (index < sql.Length && char.IsWhiteSpace(sql[index]))
                index++;

            if (index < sql.Length && sql[index] == '(')
            {
                index++;
                int columnDepth = 1;
                while (index < sql.Length && columnDepth > 0)
                {
                    if (sql[index] == '(')
                        columnDepth++;
                    else if (sql[index] == ')')
                        columnDepth--;
                    index++;
                }

                if (columnDepth != 0)
                    return false;

                while (index < sql.Length && char.IsWhiteSpace(sql[index]))
                    index++;
            }

            if (!sql.AsSpan(index).StartsWith("AS", StringComparison.OrdinalIgnoreCase))
                return false;
            index += 2;

            while (index < sql.Length && char.IsWhiteSpace(sql[index]))
                index++;
            if (index >= sql.Length || sql[index] != '(')
                return false;

            index++;
            int bodyStart = index;
            int depth = 1;
            while (index < sql.Length && depth > 0)
            {
                if (sql[index] == '(')
                    depth++;
                else if (sql[index] == ')')
                    depth--;
                index++;
            }

            if (depth != 0)
                return false;

            int bodyEnd = index - 1;
            string body = sql[bodyStart..bodyEnd].Trim();
            definitions.Add((name, body));

            while (index < sql.Length && char.IsWhiteSpace(sql[index]))
                index++;

            if (index < sql.Length && sql[index] == ',')
            {
                index++;
                continue;
            }

            mainQuery = sql[index..].Trim();
            return Regex.IsMatch(mainQuery, @"^SELECT\b", RegexOptions.IgnoreCase) && definitions.Count > 0;
        }

        return false;
    }

    private static bool TryExtractSimpleSelectSource(string cteBody, out string sourceName)
    {
        sourceName = string.Empty;

        if (Regex.IsMatch(cteBody, SimpleSubqueryForbiddenKeywordsPattern, RegexOptions.IgnoreCase))
            return false;

        Match m = Regex.Match(
            cteBody,
            @"^\s*SELECT\s+.+?\s+FROM\s+([A-Za-z_][A-Za-z0-9_\.]*)\s*(?:AS\s+[A-Za-z_][A-Za-z0-9_]*|[A-Za-z_][A-Za-z0-9_]*)?\s*$",
            RegexOptions.IgnoreCase | RegexOptions.Singleline
        );

        if (!m.Success)
            return false;

        sourceName = m.Groups[1].Value.Trim();
        return !string.IsNullOrWhiteSpace(sourceName);
    }

    private static bool TryExtractSimplePassThroughSelectSource(string sql, out string sourceName)
    {
        sourceName = string.Empty;

        if (Regex.IsMatch(sql, SimpleSubqueryForbiddenKeywordsPattern, RegexOptions.IgnoreCase))
            return false;

        Match m = Regex.Match(
            sql,
            @"^\s*SELECT\s+(?<projection>.+?)\s+FROM\s+(?<source>[A-Za-z_][A-Za-z0-9_\.]*)\s*(?:AS\s+[A-Za-z_][A-Za-z0-9_]*|[A-Za-z_][A-Za-z0-9_]*)?\s*$",
            RegexOptions.IgnoreCase | RegexOptions.Singleline
        );

        if (!m.Success)
            return false;

        if (!IsSimplePassThroughProjection(m.Groups["projection"].Value))
            return false;

        sourceName = m.Groups["source"].Value.Trim();
        return !string.IsNullOrWhiteSpace(sourceName);
    }

    private static bool IsSimplePassThroughProjection(string projection)
    {
        foreach (string rawPart in projection.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            string part = rawPart.Trim();
            if (part == "*")
                continue;

            if (Regex.IsMatch(part, @"^[A-Za-z_][A-Za-z0-9_]*\.\*$", RegexOptions.IgnoreCase))
                continue;

            if (Regex.IsMatch(part, @"^[A-Za-z_][A-Za-z0-9_]*(?:\.[A-Za-z_][A-Za-z0-9_]*)?$", RegexOptions.IgnoreCase))
                continue;

            return false;
        }

        return true;
    }

    private static bool IsIdentifierStart(char c) => char.IsLetter(c) || c == '_';

    private static bool IsIdentifierPart(char c) => char.IsLetterOrDigit(c) || c == '_';
}
