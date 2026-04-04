
namespace VisualSqlArchitect.UI.Services.QueryPreview;

internal sealed class QueryCompilationNodeGraphAssembler(
    CanvasViewModel canvas,
    DatabaseProvider provider,
    QueryCompilationCteResolver cteResolver)
{
    private readonly CanvasViewModel _canvas = canvas;
    private readonly DatabaseProvider _provider = provider;
    private readonly QueryCompilationCteResolver _cteResolver = cteResolver;

    public NodeGraph BuildNodeGraph(
        NodeViewModel resultOutputNode,
        IReadOnlyList<NodeViewModel> cteDefinitions,
        IReadOnlyDictionary<string, string> cteDefinitionNamesById,
        bool includeCtes
    )
    {
        List<NodeViewModel> scopedNodes;
        if (includeCtes)
        {
            scopedNodes = [.. _canvas.Nodes];
        }
        else
        {
            HashSet<string> upstream = CollectUpstreamNodeIds(resultOutputNode, includeCtes: false);
            scopedNodes =
            [
                .. _canvas.Nodes.Where(n =>
                    upstream.Contains(n.Id)
                    && n.Type != NodeType.CteDefinition
                ),
            ];
        }

        var scopedNodeIds = scopedNodes.Select(n => n.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var nodes = scopedNodes.Select(n => new VisualSqlArchitect.Nodes.NodeInstance(
                Id: n.Id,
                Type: n.Type,
                PinLiterals: n.PinLiterals,
                Parameters: n.Parameters,
                Alias: n.Alias,
                TableFullName: n.Type == NodeType.TableSource ? n.Subtitle : null,
                ColumnPins: n.Type == NodeType.TableSource
                    ? BuildColumnPinsMap(n)
                    : null,
                ColumnPinTypes: n.Type == NodeType.TableSource
                    ? BuildColumnPinTypesMap(n)
                    : null
            ))
            .ToList();

        var connections = _canvas
            .Connections.Where(c =>
                c.ToPin is not null
                && scopedNodeIds.Contains(c.FromPin.Owner.Id)
                && scopedNodeIds.Contains(c.ToPin!.Owner.Id)
            )
            .Select(c => new VisualSqlArchitect.Nodes.Connection(
                c.FromPin.Owner.Id,
                c.FromPin.Name,
                c.ToPin!.Owner.Id,
                c.ToPin.Name
            ))
            .ToList();

        ConnectionViewModel? columnListConn = _canvas.Connections.FirstOrDefault(c =>
            c.ToPin?.Owner == resultOutputNode
            && c.ToPin?.Name == "columns"
            && c.FromPin.Owner.Type is NodeType.ColumnList or NodeType.ColumnSetBuilder
        );

        var selectBindings = new List<VisualSqlArchitect.Nodes.SelectBinding>();

        if (columnListConn?.FromPin.Owner is NodeViewModel columnListNode)
        {
            selectBindings.AddRange(
                _canvas
                    .Connections.Where(c =>
                        c.ToPin?.Owner == columnListNode
                        && IsProjectionInputPinName(c.ToPin?.Name)
                    )
                    .OrderBy(c => c.FromPin.Name, StringComparer.Ordinal)
                    .Select(c => new VisualSqlArchitect.Nodes.SelectBinding(
                        c.FromPin.Owner.Id,
                        c.FromPin.Name,
                        c.FromPin.Owner.Alias
                    ))
            );
        }

        selectBindings.AddRange(
            _canvas
                .Connections.Where(c =>
                    c.ToPin?.Owner == resultOutputNode
                    && c.ToPin?.Name == "columns"
                    && IsWildcardProjectionPin(c.FromPin)
                )
                .Select(c => new VisualSqlArchitect.Nodes.SelectBinding(
                    c.FromPin.Owner.Id,
                    c.FromPin.Name,
                    c.FromPin.Owner.Alias
                ))
        );

        selectBindings.AddRange(
            _canvas
                .Connections.Where(c =>
                    c.ToPin?.Owner == resultOutputNode
                    && c.ToPin?.Name == "column"
                )
                .Select(c => new VisualSqlArchitect.Nodes.SelectBinding(
                    c.FromPin.Owner.Id,
                    c.FromPin.Name,
                    c.FromPin.Owner.Alias
                ))
        );

        var whereBindings = _canvas
            .Connections.Where(c => c.ToPin?.Owner == resultOutputNode && c.ToPin?.Name == "where")
            .Select(c => new VisualSqlArchitect.Nodes.WhereBinding(
                c.FromPin.Owner.Id,
                c.FromPin.Name
            ))
            .ToList();

        var havingBindings = _canvas
            .Connections.Where(c => c.ToPin?.Owner == resultOutputNode && c.ToPin?.Name == "having")
            .Select(c => new VisualSqlArchitect.Nodes.HavingBinding(
                c.FromPin.Owner.Id,
                c.FromPin.Name
            ))
            .ToList();

        var qualifyBindings = _canvas
            .Connections.Where(c => c.ToPin?.Owner == resultOutputNode && c.ToPin?.Name == "qualify")
            .Select(c => new VisualSqlArchitect.Nodes.QualifyBinding(
                c.FromPin.Owner.Id,
                c.FromPin.Name
            ))
            .ToList();

        bool distinct =
            resultOutputNode.Parameters.TryGetValue("distinct", out string? distinctValue)
            && bool.TryParse(distinctValue, out bool isDistinct)
            && isDistinct;

        string? queryHints = null;
        if (resultOutputNode.Parameters.TryGetValue("query_hints", out string? hintsRaw)
            && QueryHintSyntax.TryNormalize(_provider, hintsRaw, out string normalizedHints, out _)
            && !string.IsNullOrWhiteSpace(normalizedHints))
        {
            queryHints = normalizedHints;
        }

        string pivotMode = resultOutputNode.Parameters.TryGetValue("pivot_mode", out string? pivotModeRaw)
            ? (pivotModeRaw ?? "NONE").Trim().ToUpperInvariant()
            : "NONE";
        if (pivotMode is not ("PIVOT" or "UNPIVOT"))
            pivotMode = "NONE";

        string? pivotConfig = null;
        if (resultOutputNode.Parameters.TryGetValue("pivot_config", out string? pivotConfigRaw))
        {
            string normalizedPivotConfig = (pivotConfigRaw ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(normalizedPivotConfig))
                pivotConfig = normalizedPivotConfig;
        }

        var orderBys = new List<VisualSqlArchitect.Nodes.OrderBinding>();
        if (resultOutputNode.Parameters.TryGetValue("import_order_terms", out string? orderTermsRaw)
            && !string.IsNullOrWhiteSpace(orderTermsRaw))
        {
            string[] orderTerms = orderTermsRaw.Split(';', StringSplitOptions.RemoveEmptyEntries);
            foreach (string orderTerm in orderTerms)
            {
                string[] parts = orderTerm.Split('|', StringSplitOptions.None);
                if (parts.Length != 3)
                    continue;

                string nodeId = parts[0].Trim();
                string pinName = parts[1].Trim();
                bool descending = parts[2].Trim().Equals("DESC", StringComparison.OrdinalIgnoreCase);

                if (string.IsNullOrWhiteSpace(nodeId)
                    || string.IsNullOrWhiteSpace(pinName)
                    || !scopedNodeIds.Contains(nodeId))
                {
                    continue;
                }

                orderBys.Add(new VisualSqlArchitect.Nodes.OrderBinding(nodeId, pinName, descending));
            }
        }

        var groupBys = new List<VisualSqlArchitect.Nodes.GroupByBinding>();
        if (resultOutputNode.Parameters.TryGetValue("import_group_terms", out string? groupTermsRaw)
            && !string.IsNullOrWhiteSpace(groupTermsRaw))
        {
            string[] groupTerms = groupTermsRaw.Split(';', StringSplitOptions.RemoveEmptyEntries);
            foreach (string groupTerm in groupTerms)
            {
                string[] parts = groupTerm.Split('|', StringSplitOptions.None);
                if (parts.Length != 2)
                    continue;

                string nodeId = parts[0].Trim();
                string pinName = parts[1].Trim();

                if (string.IsNullOrWhiteSpace(nodeId)
                    || string.IsNullOrWhiteSpace(pinName)
                    || !scopedNodeIds.Contains(nodeId))
                {
                    continue;
                }

                groupBys.Add(new VisualSqlArchitect.Nodes.GroupByBinding(nodeId, pinName));
            }
        }

        int? limit = null;
        ConnectionViewModel? topConn = _canvas.Connections.FirstOrDefault(c =>
            c.ToPin?.Owner == resultOutputNode
            && c.ToPin?.Name == "top"
            && c.FromPin.Owner.Type == NodeType.Top
        );
        if (topConn is not null)
        {
            NodeViewModel topNode = topConn.FromPin.Owner;
            ConnectionViewModel? countWire = _canvas.Connections.FirstOrDefault(c =>
                c.ToPin?.Owner == topNode
                && c.ToPin?.Name == "count"
                && c.FromPin.Owner.Type == NodeType.ValueNumber
            );
            if (
                countWire is not null
                && countWire.FromPin.Owner.Parameters.TryGetValue("value", out string? wiredVal)
                && int.TryParse(wiredVal, out int wiredCount)
            )
            {
                limit = wiredCount;
            }
            else if (
                topNode.Parameters.TryGetValue("count", out string? paramVal)
                && int.TryParse(paramVal, out int paramCount)
            )
            {
                limit = paramCount;
            }
            else
            {
                limit = 100;
            }
        }

        List<VisualSqlArchitect.Nodes.CteBinding> ctes = includeCtes
            ? BuildCtes(cteDefinitions, cteDefinitionNamesById)
            : [];

        return new NodeGraph
        {
            Nodes = nodes,
            Connections = connections,
            Ctes = ctes,
            SelectOutputs = selectBindings,
            WhereConditions = whereBindings,
            Havings = havingBindings,
            Qualifies = qualifyBindings,
            QueryHints = queryHints,
            PivotMode = pivotMode,
            PivotConfig = pivotConfig,
            OrderBys = orderBys,
            GroupBys = groupBys,
            Distinct = distinct,
            Limit = limit,
        };
    }

    public static bool IsWildcardProjectionPin(PinViewModel pin)
    {
        return pin.Owner.Type == NodeType.TableSource
            && pin.EffectiveDataType == PinDataType.ColumnSet
            && pin.Name.Equals("*", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsProjectionInputPinName(string? pinName)
    {
        if (string.IsNullOrWhiteSpace(pinName))
            return false;

        return pinName.Equals("columns", StringComparison.OrdinalIgnoreCase)
            || pinName.Equals("metadata", StringComparison.OrdinalIgnoreCase);
    }

    private List<VisualSqlArchitect.Nodes.CteBinding> BuildCtes(
        IReadOnlyList<NodeViewModel> cteDefinitions,
        IReadOnlyDictionary<string, string> cteDefinitionNamesById
    )
    {
        var ctes = new List<VisualSqlArchitect.Nodes.CteBinding>();

        foreach (NodeViewModel definition in cteDefinitions)
        {
            string name = _cteResolver.ResolveDefinitionName(definition) ?? "cte_name";

            string fromTable = _cteResolver.ResolveSourceTable(definition) ?? "";

            NodeGraph cteGraph = new();
            ConnectionViewModel? queryWire = _canvas.Connections.FirstOrDefault(c =>
                c.ToPin?.Owner == definition
                && c.ToPin?.Name == "query"
                && c.FromPin.Owner.Type is NodeType.ResultOutput or NodeType.SelectOutput
            );

            if (queryWire?.FromPin.Owner is NodeViewModel cteOutput)
            {
                cteGraph = BuildNodeGraph(
                    cteOutput,
                    cteDefinitions,
                    cteDefinitionNamesById,
                    includeCtes: false
                );

                if (string.IsNullOrWhiteSpace(fromTable))
                    fromTable = _cteResolver.ResolveFromTableForOutput(cteOutput, cteDefinitionNamesById) ?? "";
            }
            else if (TryBuildPersistedCteGraph(definition, out NodeGraph persistedGraph, out string? persistedFromTable))
            {
                cteGraph = persistedGraph;
                if (string.IsNullOrWhiteSpace(fromTable))
                    fromTable = persistedFromTable ?? "";
            }

            if (string.IsNullOrWhiteSpace(fromTable))
                continue;

            bool recursive =
                definition.Parameters.TryGetValue("recursive", out string? recursiveRaw)
                && bool.TryParse(recursiveRaw, out bool recursiveFlag)
                && recursiveFlag;

            ctes.Add(new VisualSqlArchitect.Nodes.CteBinding(name, fromTable, cteGraph, recursive));
        }

        return ctes;
    }

    private bool TryBuildPersistedCteGraph(
        NodeViewModel definitionNode,
        out NodeGraph graph,
        out string? fromTable
    )
    {
        graph = new NodeGraph();
        fromTable = null;

        if (!definitionNode.Parameters.TryGetValue(CanvasSerializer.CteSubgraphParameterKey, out string? payload)
            || string.IsNullOrWhiteSpace(payload))
            return false;

        SavedCteSubgraph? subgraph;
        try
        {
            subgraph = System.Text.Json.JsonSerializer.Deserialize<SavedCteSubgraph>(payload);
        }
        catch
        {
            return false;
        }

        if (subgraph is null || subgraph.Nodes.Count == 0)
            return false;

        var tempCanvas = new CanvasViewModel();
        tempCanvas.Nodes.Clear();
        tempCanvas.Connections.Clear();
        tempCanvas.UndoRedo.Clear();

        CanvasSerializer.InsertSubgraph(
            subgraph.Nodes,
            subgraph.Connections,
            tempCanvas,
            new Avalonia.Point(0, 0)
        );

        NodeViewModel? tempResultOutput = tempCanvas.Nodes.FirstOrDefault(n =>
            n.Type is NodeType.ResultOutput or NodeType.SelectOutput
        );
        if (tempResultOutput is null)
            return false;

        var tempCteResolver = new QueryCompilationCteResolver(tempCanvas, _provider);
        var tempAssembler = new QueryCompilationNodeGraphAssembler(tempCanvas, _provider, tempCteResolver);
        List<NodeViewModel> tempCteDefinitions = tempCanvas.Nodes.Where(n => n.Type == NodeType.CteDefinition).ToList();
        Dictionary<string, string> tempCteDefinitionNamesById = tempCteResolver.BuildCteDefinitionNameMap(tempCteDefinitions);

        graph = tempAssembler.BuildNodeGraph(
            tempResultOutput,
            tempCteDefinitions,
            tempCteDefinitionNamesById,
            includeCtes: false
        );
        fromTable = tempCteResolver.ResolveFromTableForOutput(tempResultOutput, tempCteDefinitionNamesById);
        return true;
    }

    private static Dictionary<string, string> BuildColumnPinsMap(NodeViewModel node)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (PinViewModel pin in node.OutputPins)
        {
            if (string.IsNullOrWhiteSpace(pin.Name) || map.ContainsKey(pin.Name))
                continue;

            map[pin.Name] = pin.Name;
        }

        return map;
    }

    private static Dictionary<string, PinDataType> BuildColumnPinTypesMap(NodeViewModel node)
    {
        var map = new Dictionary<string, PinDataType>(StringComparer.Ordinal);
        foreach (PinViewModel pin in node.OutputPins)
        {
            if (string.IsNullOrWhiteSpace(pin.Name) || map.ContainsKey(pin.Name))
                continue;

            map[pin.Name] = pin.DataType;
        }

        return map;
    }

    private HashSet<string> CollectUpstreamNodeIds(NodeViewModel sinkNode, bool includeCtes)
    {
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { sinkNode.Id };
        var queue = new Queue<string>();
        queue.Enqueue(sinkNode.Id);

        while (queue.Count > 0)
        {
            string current = queue.Dequeue();
            foreach (ConnectionViewModel conn in _canvas.Connections.Where(c => c.ToPin?.Owner.Id == current))
            {
                NodeViewModel fromOwner = conn.FromPin.Owner;
                if (!includeCtes && fromOwner.Type == NodeType.CteDefinition)
                    continue;

                if (visited.Add(fromOwner.Id))
                    queue.Enqueue(fromOwner.Id);
            }
        }

        return visited;
    }
}



