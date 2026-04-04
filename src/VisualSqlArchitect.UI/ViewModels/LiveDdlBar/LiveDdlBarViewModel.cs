using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using VisualSqlArchitect.Core;
using VisualSqlArchitect.Ddl;
using VisualSqlArchitect.Nodes;
using VisualSqlArchitect.Compilation;
using VisualSqlArchitect.UI.Services.Localization;

namespace VisualSqlArchitect.UI.ViewModels;

/// <summary>
/// Real-time DDL preview for DDL canvas mode.
/// Phase 4 scope: CREATE TABLE generation.
/// </summary>
public sealed class LiveDdlBarViewModel : ViewModelBase
{
    private readonly CanvasViewModel _canvas;
    private readonly object _debounceLock = new();
    private readonly Dictionary<NodeViewModel, PropertyChangedEventHandler> _nodeHandlers = new();

    private string _rawSql = string.Empty;
    private bool _isValid = true;
    private bool _isCompiling;
    private string? _compileError;
    private DatabaseProvider _provider = DatabaseProvider.SqlServer;

    private CancellationTokenSource? _debounce;

    public ObservableCollection<string> ErrorHints { get; } = [];

    public DdlDiagnosticsPanelViewModel DiagnosticsPanel { get; }

    public string RawSql
    {
        get => _rawSql;
        private set => Set(ref _rawSql, value);
    }

    public bool IsValid
    {
        get => _isValid;
        private set => Set(ref _isValid, value);
    }

    public bool IsCompiling
    {
        get => _isCompiling;
        private set => Set(ref _isCompiling, value);
    }

    public string? CompileError
    {
        get => _compileError;
        private set => Set(ref _compileError, value);
    }

    public DatabaseProvider Provider
    {
        get => _provider;
        set
        {
            if (!Set(ref _provider, value))
                return;

            Recompile();
        }
    }

    public bool HasSql => !string.IsNullOrWhiteSpace(RawSql);

    public LiveDdlBarViewModel(CanvasViewModel canvas)
    {
        _canvas = canvas;
        DiagnosticsPanel = new DdlDiagnosticsPanelViewModel(nodeId => _ = FocusNodeById(nodeId));

        _canvas.Connections.CollectionChanged += OnConnectionsChanged;
        _canvas.Nodes.CollectionChanged += OnNodesChanged;

        foreach (NodeViewModel node in _canvas.Nodes)
            SubscribeNode(node);

        Recompile();
    }

    private void OnConnectionsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        ScheduleRecompile();
    }

    private void OnNodesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems is not null)
            foreach (NodeViewModel node in e.NewItems)
                SubscribeNode(node);

        if (e.OldItems is not null)
            foreach (NodeViewModel node in e.OldItems)
                UnsubscribeNode(node);

        ScheduleRecompile();
    }

    private void SubscribeNode(NodeViewModel node)
    {
        PropertyChangedEventHandler handler = (_, _) => ScheduleRecompile();
        node.PropertyChanged += handler;
        _nodeHandlers[node] = handler;
    }

    private void UnsubscribeNode(NodeViewModel node)
    {
        if (!_nodeHandlers.TryGetValue(node, out PropertyChangedEventHandler? handler))
            return;

        node.PropertyChanged -= handler;
        _nodeHandlers.Remove(node);
    }

    private void ScheduleRecompile()
    {
        lock (_debounceLock)
        {
            _debounce?.Cancel();
            _debounce?.Dispose();
            _debounce = new CancellationTokenSource();
            CancellationToken token = _debounce.Token;

            Task.Delay(120, token)
                .ContinueWith(_ =>
                {
                    if (!token.IsCancellationRequested)
                        Avalonia.Threading.Dispatcher.UIThread.Post(Recompile);
                }, TaskScheduler.Default);
        }
    }

    public void Recompile()
    {
        ErrorHints.Clear();
        IsCompiling = true;

        try
        {
            NodeGraph graph = BuildNodeGraph();
            var adapter = new DdlGraphCompilerAdapter(Provider);

            if (!adapter.TryCompile(graph, out var statements, out var errors))
            {
                RawSql = string.Empty;
                IsValid = false;
                CompileError = errors.FirstOrDefault() ?? L("ddl.compilationFailed", "Compilation failed");
                ErrorHints.Add(CompileError);
                DiagnosticsPanel.ReplaceDiagnostics([]);
                return;
            }

            var generator = new DdlGeneratorService(Provider);
            RawSql = generator.Generate(statements ?? []);
            IsValid = true;
            CompileError = null;
        }
        catch (Exception ex)
        {
            RawSql = string.Empty;
            IsValid = false;
            CompileError = ex.Message;
            DiagnosticsPanel.ReplaceDiagnostics([
                new DdlCompileDiagnostic(
                    "E-DDL-COMPILE-UNEXPECTED",
                    DdlDiagnosticSeverity.Error,
                    ex.Message
                ),
            ]);
            ErrorHints.Add(string.Format(L("ddl.compileErrorWithReason", "DDL compile error: {0}"), ex.Message));
        }
        finally
        {
            IsCompiling = false;
            RaisePropertyChanged(nameof(HasSql));
        }
    }

    private NodeGraph BuildNodeGraph()
    {
        IReadOnlyList<NodeInstance> nodes =
        [
            .. _canvas.Nodes.Select(n => new NodeInstance(
                n.Id,
                n.Type,
                new Dictionary<string, string>(),
                new Dictionary<string, string>(n.Parameters),
                n.Alias,
                n.Type == NodeType.TableSource ? n.Subtitle : null,
                null,
                null
            )),
        ];

        IReadOnlyList<Connection> connections =
        [
            .. _canvas.Connections
                .Where(c => c.ToPin is not null)
                .Select(c => new Connection(
                    c.FromPin.Owner.Id,
                    c.FromPin.Name,
                    c.ToPin!.Owner.Id,
                    c.ToPin.Name
                )),
        ];

        return new NodeGraph
        {
            Nodes = nodes,
            Connections = connections,
        };
    }

    private bool FocusNodeById(string nodeId)
    {
        NodeViewModel? target = _canvas.Nodes.FirstOrDefault(n => n.Id == nodeId);
        if (target is null)
            return false;

        foreach (NodeViewModel node in _canvas.Nodes)
            node.IsSelected = ReferenceEquals(node, target);

        return true;
    }

    private static string L(string key, string fallback)
    {
        string value = LocalizationService.Instance[key];
        return string.Equals(value, key, StringComparison.Ordinal) ? fallback : value;
    }
}
