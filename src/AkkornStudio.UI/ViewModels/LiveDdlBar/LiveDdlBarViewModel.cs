using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using AkkornStudio.Core;
using AkkornStudio.Ddl;
using AkkornStudio.Ddl.SchemaAnalysis.Application;
using AkkornStudio.Ddl.SchemaAnalysis.Application.Validation;
using AkkornStudio.Nodes;
using AkkornStudio.Compilation;
using AkkornStudio.Metadata;
using AkkornStudio.UI.Services.Localization;

namespace AkkornStudio.UI.ViewModels;

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
    private readonly SchemaAnalysisService _schemaAnalysisService;
    private CancellationTokenSource? _schemaAnalysisCts;
    private bool _isRunningSchemaAnalysis;

    public ObservableCollection<string> ErrorHints { get; } = [];

    public DdlDiagnosticsPanelViewModel DiagnosticsPanel { get; }

    public SchemaAnalysisPanelViewModel SchemaAnalysisPanel { get; }

    public RelayCommand RunSchemaAnalysisCommand { get; }

    public RelayCommand CancelSchemaAnalysisCommand { get; }

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

    public bool IsRunningSchemaAnalysis
    {
        get => _isRunningSchemaAnalysis;
        private set
        {
            if (!Set(ref _isRunningSchemaAnalysis, value))
                return;

            RunSchemaAnalysisCommand.NotifyCanExecuteChanged();
            CancelSchemaAnalysisCommand.NotifyCanExecuteChanged();
        }
    }

    public LiveDdlBarViewModel(CanvasViewModel canvas)
    {
        _canvas = canvas;
        _schemaAnalysisService = SchemaAnalysisServiceFactory.CreateDefault();
        DiagnosticsPanel = new DdlDiagnosticsPanelViewModel(nodeId => _ = FocusNodeById(nodeId));
        SchemaAnalysisPanel = new SchemaAnalysisPanelViewModel();
        RunSchemaAnalysisCommand = new RelayCommand(
            () => _ = AnalyzeSchemaStructureAsync(),
            () => !IsRunningSchemaAnalysis && _canvas.DatabaseMetadata is not null
        );
        CancelSchemaAnalysisCommand = new RelayCommand(
            CancelSchemaAnalysis,
            () => IsRunningSchemaAnalysis
        );

        _canvas.Connections.CollectionChanged += OnConnectionsChanged;
        _canvas.Nodes.CollectionChanged += OnNodesChanged;
        _canvas.PropertyChanged += OnCanvasPropertyChanged;

        foreach (NodeViewModel node in _canvas.Nodes)
            SubscribeNode(node);

        Recompile();

        if (_canvas.DatabaseMetadata is null)
            SchemaAnalysisPanel.SetMetadataUnavailable();
    }

    private void OnCanvasPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CanvasViewModel.DatabaseMetadata))
        {
            RunSchemaAnalysisCommand.NotifyCanExecuteChanged();
            if (_canvas.DatabaseMetadata is null)
                SchemaAnalysisPanel.SetMetadataUnavailable();
        }
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

    public async Task AnalyzeSchemaStructureAsync(CancellationToken ct = default)
    {
        if (IsRunningSchemaAnalysis)
            return;

        if (_canvas.DatabaseMetadata is null)
        {
            SchemaAnalysisPanel.SetMetadataUnavailable();
            return;
        }

        _schemaAnalysisCts?.Cancel();
        _schemaAnalysisCts?.Dispose();
        _schemaAnalysisCts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        try
        {
            IsRunningSchemaAnalysis = true;
            SchemaAnalysisPanel.SetLoading();

            DbMetadata analysisMetadata = BuildAnalysisMetadata(_canvas.DatabaseMetadata, SchemaAnalysisPanel);
            var profile = SchemaAnalysisProfileNormalizer.CreateDefaultProfile();
            var result = await _schemaAnalysisService.AnalyzeAsync(
                analysisMetadata,
                profile,
                _schemaAnalysisCts.Token
            );

            SchemaAnalysisPanel.ApplyResult(result);
        }
        catch (OperationCanceledException)
        {
            SchemaAnalysisPanel.SetCancelled();
        }
        finally
        {
            IsRunningSchemaAnalysis = false;
        }
    }

    public void CancelSchemaAnalysis()
    {
        _schemaAnalysisCts?.Cancel();
    }

    private static DbMetadata BuildAnalysisMetadata(DbMetadata metadata, SchemaAnalysisPanelViewModel panel)
    {
        IReadOnlyList<SchemaMetadata> filteredSchemas = metadata.Schemas
            .Select(schema =>
            {
                IReadOnlyList<TableMetadata> tables = schema.Tables
                    .Where(table => !panel.ShouldIgnoreTableForAnalysis(schema.Name, table.Name, table.Kind))
                    .ToList();

                return new SchemaMetadata(schema.Name, tables);
            })
            .Where(schema => schema.Tables.Count > 0)
            .ToList();

        if (filteredSchemas.Count == metadata.Schemas.Count
            && filteredSchemas.SelectMany(static schema => schema.Tables).Count() == metadata.AllTables.Count())
        {
            return metadata;
        }

        HashSet<string> survivingTables = filteredSchemas
            .SelectMany(schema => schema.Tables.Select(table => QualifyTable(schema.Name, table.Name)))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        IReadOnlyList<ForeignKeyRelation> filteredForeignKeys = metadata.AllForeignKeys
            .Where(foreignKey =>
                survivingTables.Contains(QualifyTable(foreignKey.ChildSchema, foreignKey.ChildTable))
                && survivingTables.Contains(QualifyTable(foreignKey.ParentSchema, foreignKey.ParentTable)))
            .ToList();

        return metadata with
        {
            Schemas = filteredSchemas,
            AllForeignKeys = filteredForeignKeys,
        };
    }

    private static string QualifyTable(string? schema, string table)
    {
        return string.IsNullOrWhiteSpace(schema)
            ? table
            : $"{schema}.{table}";
    }

    private NodeGraph BuildNodeGraph()
    {
        var nodes = _canvas.Nodes.Select(n => new NodeInstance(
            n.Id,
            n.Type,
            new Dictionary<string, string>(),
            new Dictionary<string, string>(n.Parameters),
            n.Alias,
            n.Type == NodeType.TableSource ? n.Subtitle : null,
            null,
            null
        )).ToList();

        var connections = _canvas.Connections
            .Where(c => c.ToPin is not null)
            .Select(c => new Connection(
                c.FromPin.Owner.Id,
                c.FromPin.Name,
                c.ToPin!.Owner.Id,
                c.ToPin.Name
            ))
            .ToList();

        AddImplicitOutputsIfMissing(nodes, connections);

        return new NodeGraph
        {
            Nodes = nodes,
            Connections = connections,
        };
    }

    private static void AddImplicitOutputsIfMissing(List<NodeInstance> nodes, List<Connection> connections)
    {
        bool hasOutput = nodes.Any(n => n.Type is NodeType.CreateTableOutput
            or NodeType.CreateTypeOutput
            or NodeType.CreateSequenceOutput
            or NodeType.CreateTableAsOutput
            or NodeType.CreateViewOutput
            or NodeType.AlterViewOutput
            or NodeType.CreateIndexOutput
            or NodeType.AlterTableOutput);

        if (hasOutput)
            return;

        AddImplicitOutputForDefinitions(
            nodes,
            connections,
            sourceType: NodeType.TableDefinition,
            outputType: NodeType.CreateTableOutput,
            sourcePin: "table",
            outputPin: "table"
        );
        AddImplicitOutputForDefinitions(
            nodes,
            connections,
            sourceType: NodeType.EnumTypeDefinition,
            outputType: NodeType.CreateTypeOutput,
            sourcePin: "type_def",
            outputPin: "type_def"
        );
        AddImplicitOutputForDefinitions(
            nodes,
            connections,
            sourceType: NodeType.SequenceDefinition,
            outputType: NodeType.CreateSequenceOutput,
            sourcePin: "seq",
            outputPin: "seq"
        );
        AddImplicitOutputForDefinitions(
            nodes,
            connections,
            sourceType: NodeType.ViewDefinition,
            outputType: NodeType.CreateViewOutput,
            sourcePin: "view",
            outputPin: "view"
        );
    }

    private static void AddImplicitOutputForDefinitions(
        List<NodeInstance> nodes,
        List<Connection> connections,
        NodeType sourceType,
        NodeType outputType,
        string sourcePin,
        string outputPin)
    {
        foreach (NodeInstance source in nodes.Where(n => n.Type == sourceType).ToList())
        {
            string outputId = $"__auto_output_{outputType}_{source.Id}";
            nodes.Add(new NodeInstance(
                outputId,
                outputType,
                new Dictionary<string, string>(),
                new Dictionary<string, string>(),
                null,
                null,
                null,
                null
            ));

            connections.Add(new Connection(
                source.Id,
                sourcePin,
                outputId,
                outputPin
            ));
        }
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
