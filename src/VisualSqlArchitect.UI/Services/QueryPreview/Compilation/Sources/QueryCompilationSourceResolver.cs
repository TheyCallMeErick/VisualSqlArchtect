namespace VisualSqlArchitect.UI.Services.QueryPreview;

internal sealed class QueryCompilationSourceResolver(
    CanvasViewModel canvas,
    Func<NodeViewModel, IReadOnlyDictionary<string, string>, string?> resolveCteSourceReference)
{
    private readonly CanvasViewModel _canvas = canvas;
    private readonly Func<NodeViewModel, IReadOnlyDictionary<string, string>, string?> _resolveCteSourceReference = resolveCteSourceReference;

    public (string FromTable, string? Warning) ResolveFromTable(
        IReadOnlyList<NodeViewModel> tableNodes,
        IReadOnlyList<NodeViewModel> cteSourceNodes,
        IReadOnlyList<NodeViewModel> subqueryNodes,
        IReadOnlyDictionary<string, string> cteDefinitionNamesById)
    {
        if (tableNodes.Count > 0)
            return (tableNodes[0].Subtitle ?? tableNodes[0].Title, null);

        if (cteSourceNodes.Count > 0)
        {
            NodeViewModel cte = cteSourceNodes[0];
            string? cteReference = _resolveCteSourceReference(cte, cteDefinitionNamesById);
            if (!string.IsNullOrWhiteSpace(cteReference))
                return (cteReference, null);
        }

        if (subqueryNodes.Count > 0)
        {
            (string? subqueryFrom, string? warning) = ResolveSubqueryFromSource(subqueryNodes[0]);
            if (!string.IsNullOrWhiteSpace(subqueryFrom))
                return (subqueryFrom, warning);

            return ("cte_name", warning);
        }

        return ("cte_name", null);
    }

    private (string? FromSource, string? Warning) ResolveSubqueryFromSource(NodeViewModel subqueryNode)
    {
        string? query = QueryGraphHelpers.ResolveTextInput(_canvas, subqueryNode, "query_text");
        if (string.IsNullOrWhiteSpace(query)
            && subqueryNode.Parameters.TryGetValue("query", out string? byParam)
            && !string.IsNullOrWhiteSpace(byParam))
        {
            query = byParam;
        }

        if (string.IsNullOrWhiteSpace(query))
            return (null, "Subquery source is missing query SQL. Add a SELECT or WITH query.");

        string body = query.Trim().TrimEnd(';');
        if (!QueryGraphHelpers.LooksLikeSelectStatement(body))
        {
            return (
                null,
                "Subquery source must start with SELECT, WITH, or a parenthesized SELECT. Ignoring Subquery source."
            );
        }

        if (!(body.StartsWith("(", StringComparison.Ordinal) && body.EndsWith(")", StringComparison.Ordinal)))
            body = $"({body})";

        string? alias = QueryGraphHelpers.ResolveTextInput(_canvas, subqueryNode, "alias_text");
        if (string.IsNullOrWhiteSpace(alias)
            && subqueryNode.Parameters.TryGetValue("alias", out string? aliasParam)
            && !string.IsNullOrWhiteSpace(aliasParam))
        {
            alias = aliasParam;
        }

        string? warning = null;
        if (string.IsNullOrWhiteSpace(alias))
        {
            alias = "subq";
            warning = "Subquery source alias is required. Defaulting alias to 'subq'.";
        }
        else
        {
            alias = alias.Trim();
            if (alias.Contains(' ', StringComparison.Ordinal))
            {
                warning = "Subquery source alias cannot contain spaces. Defaulting alias to 'subq'.";
                alias = "subq";
            }
        }

        return ($"{body} {alias}", warning);
    }
}


