using System.Text.Json;
using VisualSqlArchitect.CanvasKit;
using VisualSqlArchitect.Core;
using VisualSqlArchitect.Nodes;
using VisualSqlArchitect.UI.Services.Localization;
using VisualSqlArchitect.UI.Serialization;
using VisualSqlArchitect.UI.ViewModels.Canvas;
using VisualSqlArchitect.UI.ViewModels.Canvas.Strategies;
using VisualSqlArchitect.UI.Services.QueryPreview;

namespace VisualSqlArchitect.UI.ViewModels;

/// <summary>
/// Manages CTE and View sub-editor sessions, removing that responsibility
/// from CanvasViewModel (SRP). Exposes session state as plain properties;
/// CanvasViewModel forwards them to the UI layer.
/// </summary>
internal sealed class SubCanvasEditingController
{
    private sealed record CteEditorSession(
        string ParentCanvasJson,
        string ParentCteNodeId,
        bool ParentWasDirty,
        string CteDisplayName
    );

    private sealed record ViewEditorSession(
        string ParentCanvasJson,
        string ParentViewNodeId,
        bool ParentWasDirty,
        string ViewDisplayName
    );

    private readonly CanvasViewModel _canvas;
    private readonly ICanvasDomainStrategy _domainStrategy;
    private readonly ISelectionManager _selectionManager;
    private readonly AppDiagnosticsViewModel _diagnostics;
    private readonly ILocalizationService _localizationService;
    private readonly Action _notifyStateChanged;
    private readonly Func<object?>? _getCteSession;
    private readonly Action<object?>? _setCteSession;
    private readonly Func<object?>? _getViewSession;
    private readonly Action<object?>? _setViewSession;

    private CteEditorSession? _cteEditorSession;
    private ViewEditorSession? _viewEditorSession;
    private CanvasSubEditorSessionState _sessionState = CanvasSubEditorSessionState.Empty;

    private CteEditorSession? CteSession
    {
        get => _getCteSession is not null ? _getCteSession() as CteEditorSession : _cteEditorSession;
        set
        {
            _cteEditorSession = value;
            _setCteSession?.Invoke(value);
        }
    }

    private ViewEditorSession? ViewSession
    {
        get => _getViewSession is not null ? _getViewSession() as ViewEditorSession : _viewEditorSession;
        set
        {
            _viewEditorSession = value;
            _setViewSession?.Invoke(value);
        }
    }

    public SubCanvasEditingController(
        CanvasViewModel canvas,
        ICanvasDomainStrategy domainStrategy,
        ISelectionManager selectionManager,
        AppDiagnosticsViewModel diagnostics,
        ILocalizationService localizationService,
        Action notifyStateChanged,
        Func<object?>? getCteSession = null,
        Action<object?>? setCteSession = null,
        Func<object?>? getViewSession = null,
        Action<object?>? setViewSession = null)
    {
        _canvas = canvas;
        _domainStrategy = domainStrategy;
        _selectionManager = selectionManager;
        _diagnostics = diagnostics;
        _localizationService = localizationService;
        _notifyStateChanged = notifyStateChanged;
        _getCteSession = getCteSession;
        _setCteSession = setCteSession;
        _getViewSession = getViewSession;
        _setViewSession = setViewSession;
    }

    public bool IsInCteEditor => _sessionState.IsActive;
    public bool IsInViewEditor => _sessionState.IsViewEditor;
    public bool IsCanvasDimmedBySubcanvas => IsInViewEditor;

    public string CteEditorBreadcrumb =>
        _sessionState.Mode switch
        {
            CanvasSubEditorMode.Cte =>
                $"{_localizationService["main.cteEditor.editingPrefix"]}{_sessionState.DisplayName}",
            CanvasSubEditorMode.View =>
                $"{_localizationService["main.viewEditor.editingPrefix"]}{_sessionState.DisplayName}",
            _ => string.Empty,
        };

    public string EditorExitLabel =>
        IsInViewEditor
            ? _localizationService["main.viewEditor.backToCanvas"]
            : _localizationService["main.cteEditor.backToCanvas"];

    public string EditorExitA11y =>
        IsInViewEditor
            ? _localizationService["main.viewEditor.exitA11y"]
            : _localizationService["main.cteEditor.exitA11y"];

    public bool CanEnterSelectedCteEditor()
    {
        if (IsInCteEditor)
            return false;

        return _canvas.Nodes.Count(n => n.IsSelected && _domainStrategy.CanEnterSubEditor(n)) == 1;
    }

    public async Task<bool> EnterSelectedCteEditorAsync()
    {
        if (!CanEnterSelectedCteEditor())
            return false;

        NodeViewModel selectedNode = _canvas.Nodes.Single(n => n.IsSelected && _domainStrategy.CanEnterSubEditor(n));
        if (_domainStrategy.CanEnterSubEditor(selectedNode) && selectedNode.Parameters.ContainsKey("ViewName"))
            return await EnterViewEditorAsync(selectedNode);

        NodeViewModel cteNode = selectedNode;
        string cteName = ResolveCteDisplayName(cteNode);

        string parentJson = CanvasSerializer.Serialize(_canvas);
        bool parentWasDirty = _canvas.IsDirty;

        (List<SavedNode> nodes, List<SavedConnection> conns) = ExtractCteEditableSubgraph(cteNode);
        CanvasLoadResult load = await LoadSubEditorCanvasAsync(
            cteNode, nodes, conns, $"CTE editor: {cteName}"
        );
        if (!load.Success)
            return false;

        CteSession = new CteEditorSession(parentJson, cteNode.Id, parentWasDirty, cteName);
        _sessionState = CanvasSubEditorStateMachine.EnterCte(cteName);
        _canvas.IsDirty = false;
        _notifyStateChanged();
        return true;
    }

    public async Task<bool> EnterCteEditorAsync(NodeViewModel cteNode)
    {
        if (IsInCteEditor)
            return false;
        if (!_domainStrategy.CanEnterSubEditor(cteNode))
            return false;
        if (!_canvas.Nodes.Contains(cteNode))
            return false;

        _selectionManager.DeselectAll();
        cteNode.IsSelected = true;
        return await EnterSelectedCteEditorAsync();
    }

    public async Task<bool> ExitCteEditorAsync(bool forceDiscard = false)
    {
        if (ViewSession is not null)
            return await ExitViewEditorAsync(forceDiscard);

        if (CteSession is null)
            return false;

        CteEditorSession session = CteSession;
        (List<SavedNode> editedNodes, List<SavedConnection> editedConnections) =
            CanvasSerializer.SerialiseSubgraph(_canvas.Nodes, _canvas.Connections);

        CanvasLoadResult restore = CanvasSerializer.Deserialize(session.ParentCanvasJson, _canvas);
        if (!restore.Success)
        {
            CteSession = null;
            _sessionState = CanvasSubEditorStateMachine.Exit();
            _diagnostics.AddWarning(
                area: L("diagnostics.area.cteEditor", "CTE Editor"),
                message: L("diagnostics.cteEditor.restoreParentFailed", "Failed to restore the parent canvas. CTE edits were discarded."),
                recommendation: L("diagnostics.recommendation.reloadFileIfNeeded", "Reload the file if needed."),
                openPanel: true
            );
            _notifyStateChanged();
            return false;
        }

        NodeViewModel? cteNode = _canvas.Nodes.FirstOrDefault(n =>
            n.Id == session.ParentCteNodeId && _domainStrategy.CanEnterSubEditor(n)
        );
        if (cteNode is null)
        {
            CteSession = null;
            _sessionState = CanvasSubEditorStateMachine.Exit();
            _notifyStateChanged();
            return false;
        }

        RemoveExistingCteQuerySubgraph(cteNode);
        PersistCteSubgraph(_domainStrategy, cteNode, editedNodes, editedConnections);

        CteSession = null;
        _sessionState = CanvasSubEditorStateMachine.Exit();
        _canvas.IsDirty = true;
        _notifyStateChanged();
        return true;
    }

    public async Task<bool> EnterViewEditorAsync(NodeViewModel viewNode)
    {
        if (IsInCteEditor)
            return false;
        if (!_domainStrategy.CanEnterSubEditor(viewNode))
            return false;
        if (!_canvas.Nodes.Contains(viewNode))
            return false;

        string parentJson = CanvasSerializer.Serialize(_canvas);
        bool parentWasDirty = _canvas.IsDirty;
        string viewName = ResolveViewDisplayName(viewNode);

        (List<SavedNode> nodes, List<SavedConnection> conns) = ExtractViewEditableSubgraph(viewNode);
        CanvasLoadResult load = await LoadSubEditorCanvasAsync(
            viewNode, nodes, conns, $"View editor: {viewName}"
        );
        if (!load.Success)
            return false;

        ViewSession = new ViewEditorSession(parentJson, viewNode.Id, parentWasDirty, viewName);
        _sessionState = CanvasSubEditorStateMachine.EnterView(viewName);
        _canvas.IsDirty = false;
        _notifyStateChanged();
        return true;
    }

    private Task<bool> ExitViewEditorAsync(bool forceDiscard = false)
    {
        if (ViewSession is null)
            return Task.FromResult(false);

        ViewEditorSession session = ViewSession;
        QueryGraphBuilder builder = new(_canvas, _canvas.ActiveConnectionConfig?.Provider ?? DatabaseProvider.Postgres);
        bool graphBuilt = builder.TryBuildGraphSnapshot(
            out NodeGraph graph,
            out string? fromTable,
            out string? selectSql,
            out List<string> buildErrors
        );
        if (!graphBuilt && !forceDiscard)
        {
            string? buildError = buildErrors.FirstOrDefault();
            _diagnostics.AddWarning(
                area: L("diagnostics.area.viewEditor", "View Editor"),
                message: string.Format(
                    L("diagnostics.viewEditor.exitFailed", "Could not exit: {0}"),
                    buildError ?? L("diagnostics.viewEditor.canvasIncomplete", "the canvas is incomplete.")
                ),
                recommendation: L("diagnostics.viewEditor.exitRecommendation", "Connect a valid ResultOutput or use the discard command."),
                openPanel: true
            );
            return Task.FromResult(false);
        }

        CanvasLoadResult restore = CanvasSerializer.Deserialize(session.ParentCanvasJson, _canvas);
        if (!restore.Success)
        {
            ViewSession = null;
            _sessionState = CanvasSubEditorStateMachine.Exit();
            _diagnostics.AddWarning(
                area: L("diagnostics.area.viewEditor", "View Editor"),
                message: L("diagnostics.viewEditor.restoreParentFailed", "Failed to restore the parent canvas. The subgraph was discarded."),
                recommendation: L("diagnostics.recommendation.reloadFileIfNeeded", "Reload the file if needed."),
                openPanel: true
            );
            _notifyStateChanged();
            return Task.FromResult(false);
        }

        NodeViewModel? viewNode = _canvas.Nodes.FirstOrDefault(n =>
            n.Id == session.ParentViewNodeId && _domainStrategy.CanEnterSubEditor(n)
        );
        if (viewNode is null)
        {
            ViewSession = null;
            _sessionState = CanvasSubEditorStateMachine.Exit();
            _notifyStateChanged();
            return Task.FromResult(false);
        }

        if (graphBuilt)
        {
            viewNode.Parameters[CanvasSerializer.ViewSubgraphParameterKey] = JsonSerializer.Serialize(graph);
            if (!string.IsNullOrWhiteSpace(fromTable))
                viewNode.Parameters[CanvasSerializer.ViewFromTableParameterKey] = fromTable;
            if (!string.IsNullOrWhiteSpace(selectSql))
                viewNode.Parameters["SelectSql"] = selectSql;
        }

        ViewSession = null;
        _sessionState = CanvasSubEditorStateMachine.Exit();
        _canvas.IsDirty = true;
        _notifyStateChanged();
        return Task.FromResult(true);
    }

    private static string ResolveCteDisplayName(NodeViewModel cteNode)
    {
        if (cteNode.Parameters.TryGetValue("name", out string? name) && !string.IsNullOrWhiteSpace(name))
            return name.Trim();

        if (cteNode.Parameters.TryGetValue("cte_name", out string? legacy) && !string.IsNullOrWhiteSpace(legacy))
            return legacy.Trim();

        return cteNode.Title;
    }

    private static string ResolveViewDisplayName(NodeViewModel viewNode)
    {
        if (viewNode.Parameters.TryGetValue("ViewName", out string? name) && !string.IsNullOrWhiteSpace(name))
            return name.Trim();

        return viewNode.Title;
    }

    private static (List<SavedNode> Nodes, List<SavedConnection> Connections) ExtractViewEditableSubgraph(NodeViewModel viewNode)
    {
        if (!viewNode.Parameters.TryGetValue(CanvasSerializer.ViewSubgraphParameterKey, out string? payload)
            || string.IsNullOrWhiteSpace(payload))
            return ([], []);

        NodeGraph? graph;
        try { graph = JsonSerializer.Deserialize<NodeGraph>(payload); }
        catch { return ([], []); }

        if (graph is null || graph.Nodes.Count == 0)
            return ([], []);

        List<SavedNode> nodes = graph.Nodes.Select(n =>
        {
            List<SavedColumn>? columns = null;
            if (!string.IsNullOrWhiteSpace(n.TableFullName) && n.ColumnPins is { Count: > 0 })
            {
                columns = n.ColumnPins
                    .Select(cp =>
                    {
                        string typeLabel = "ColumnRef";
                        if (n.ColumnPinTypes is not null && n.ColumnPinTypes.TryGetValue(cp.Value, out PinDataType t))
                            typeLabel = t.ToString();

                        return new SavedColumn(cp.Key, typeLabel);
                    })
                    .ToList();
            }

            return new SavedNode(
                NodeId: n.Id,
                NodeType: n.Type.ToString(),
                X: 140, Y: 120, ZOrder: null,
                Alias: n.Alias,
                TableFullName: !string.IsNullOrWhiteSpace(n.TableFullName) ? n.TableFullName : null,
                Parameters: new Dictionary<string, string>(n.Parameters),
                PinLiterals: new Dictionary<string, string>(n.PinLiterals),
                Columns: columns
            );
        }).ToList();

        for (int i = 0; i < nodes.Count; i++)
        {
            SavedNode n = nodes[i];
            nodes[i] = n with
            {
                X = 120 + ((i % 4) * 220),
                Y = 80 + ((i / 4) * 170),
            };
        }

        List<SavedConnection> connections = graph.Connections
            .Select(c => new SavedConnection(c.FromNodeId, c.FromPinName, c.ToNodeId, c.ToPinName))
            .ToList();

        return (nodes, connections);
    }

    private (List<SavedNode> Nodes, List<SavedConnection> Connections) ExtractCteEditableSubgraph(NodeViewModel cteNode)
        => _domainStrategy is ICanvasDomainStrategyExt ext
            ? ext.ExtractCteEditableSubgraph(cteNode, _canvas.Nodes, _canvas.Connections)
            : ([], []);

    private static void PersistCteSubgraph(
        ICanvasDomainStrategy domainStrategy,
        NodeViewModel cteNode,
        List<SavedNode> editedNodes,
        List<SavedConnection> editedConnections)
    {
        string? resultOutputNodeId = domainStrategy is ICanvasDomainStrategyExt ext
            ? ext.ResolvePrimaryOutputNodeId(editedNodes)
            : null;

        var subgraph = new SavedCteSubgraph(
            Nodes: editedNodes,
            Connections: editedConnections,
            ResultOutputNodeId: resultOutputNodeId
        );

        cteNode.Parameters[CanvasSerializer.CteSubgraphParameterKey] = JsonSerializer.Serialize(subgraph);
    }

    private void RemoveExistingCteQuerySubgraph(NodeViewModel cteNode)
    {
        if (_domainStrategy is ICanvasDomainStrategyExt ext)
            ext.RemoveExistingCteQuerySubgraph(cteNode, _canvas.Nodes, _canvas.Connections);
    }

    private async Task<CanvasLoadResult> LoadSubEditorCanvasAsync(
        NodeViewModel editorNode,
        List<SavedNode> nodes,
        List<SavedConnection> connections,
        string description)
    {
        if (nodes.Count > 0)
            return CanvasSerializer.Deserialize(
                BuildTemporaryCanvasJson(nodes, connections, description),
                _canvas
            );

        CanvasSnapshot? seed = await _domainStrategy.GetSubEditorSeedAsync(editorNode).ConfigureAwait(true);
        if (seed is null || string.IsNullOrWhiteSpace(seed.JsonPayload))
            return CanvasLoadResult.Fail(
                string.Format(
                    L("main.subEditor.noSeedProvided", "No {0} sub-editor seed was provided."),
                    _domainStrategy.DomainName
                )
            );

        return CanvasSerializer.Deserialize(seed.JsonPayload, _canvas);
    }

    private static string BuildTemporaryCanvasJson(
        List<SavedNode> nodes,
        List<SavedConnection> connections,
        string description)
    {
        var saved = new SavedCanvas(
            Version: CanvasSerializer.LegacyCanvasSchemaVersion,
            DatabaseProvider: "Postgres",
            ConnectionName: "cte-editor",
            Zoom: 1.0, PanX: 0, PanY: 0,
            Nodes: nodes,
            Connections: connections,
            SelectBindings: [],
            WhereBindings: [],
            AppVersion: CanvasSerializer.AppVersion,
            CreatedAt: DateTime.UtcNow.ToString("o"),
            Description: description
        );

        return JsonSerializer.Serialize(saved);
    }

    public async Task RunSubEditorActionSafeAsync(Func<Task<bool>> operation)
    {
        try
        {
            await operation();
        }
        catch (Exception ex)
        {
            _diagnostics.AddWarning(
                area: L("diagnostics.area.subEditor", "Sub-editor"),
                message: string.Format(
                    L("diagnostics.subEditor.executeFailed", "Failed to execute editor action: {0}"),
                    ex.Message
                ),
                recommendation: L("diagnostics.subEditor.executeRecommendation", "Try again. If it persists, reload the canvas."),
                openPanel: true
            );
        }
    }

    private string L(string key, string fallback)
    {
        string value = _localizationService[key];
        return string.Equals(value, key, StringComparison.Ordinal) ? fallback : value;
    }
}

