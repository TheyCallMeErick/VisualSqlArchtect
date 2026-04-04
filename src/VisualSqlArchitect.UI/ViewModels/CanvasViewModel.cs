using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Collections.Specialized;
using System.Text.Json;
using Avalonia;
using VisualSqlArchitect.CanvasKit;
using VisualSqlArchitect.Core;
using VisualSqlArchitect.Metadata;
using VisualSqlArchitect.Nodes;
using VisualSqlArchitect.UI.Services.Export;
using VisualSqlArchitect.UI.Services.Localization;
using VisualSqlArchitect.UI.Serialization;
using VisualSqlArchitect.UI.ViewModels.Canvas;
using VisualSqlArchitect.UI.ViewModels.Canvas.Strategies;
using VisualSqlArchitect.UI.Services.QueryPreview;
using VisualSqlArchitect.UI.ViewModels.UndoRedo;
using VisualSqlArchitect.UI.ViewModels.UndoRedo.Commands;
using VisualSqlArchitect.UI.ViewModels.Validation.Conventions;

namespace VisualSqlArchitect.UI.ViewModels;

/// <summary>
/// Facade for all canvas operations.
/// Coordinates UI interactions by delegating to specialised managers:
///   - <see cref="NodeManager"/>       â€” node lifecycle (spawn, delete, demo)
///   - <see cref="PinManager"/>         â€” connections and type narrowing
///   - <see cref="SelectionManager"/>   â€” node selection and alignment
///   - <see cref="NodeLayoutManager"/>  â€” zoom, pan, snap, auto-layout
///   - <see cref="ValidationManager"/>  â€” graph validation and orphan detection
/// </summary>
public sealed class CanvasViewModel : ViewModelBase, IDisposable
{
    // â”€â”€ Core collections â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    public ObservableCollection<NodeViewModel> Nodes { get; } = [];
    public ObservableCollection<ConnectionViewModel> Connections { get; } = [];

    public bool IsEmpty => Nodes.Count == 0 && Connections.Count == 0;

    // â”€â”€ Child view models â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    public SearchMenuViewModel SearchMenu { get; }
    public DataPreviewViewModel DataPreview { get; } = new();
    public ToastCenterViewModel Toasts { get; }
    public AppDiagnosticsViewModel Diagnostics { get; }
    public PropertyPanelViewModel PropertyPanel { get; }
    public LiveSqlBarViewModel LiveSql { get; set; }
    public AutoJoinOverlayViewModel AutoJoin { get; set; }
    public ManualJoinDialogViewModel ManualJoinDialog { get; }
    public UndoRedoStack UndoRedo { get; }
    public ConnectionManagerViewModel ConnectionManager { get; } = new();
    public BenchmarkViewModel Benchmark { get; private set; } = null!;
    public ExplainPlanViewModel ExplainPlan { get; private set; } = null!;
    public SqlImporterViewModel SqlImporter { get; private set; } = null!;
    public FlowVersionOverlayViewModel FlowVersions { get; private set; } = null!;
    public FileVersionHistoryViewModel FileHistory { get; private set; } = null!;
    public SidebarViewModel Sidebar { get; private set; } = null!;

    private LiveDdlBarViewModel? _liveDdl;
    public LiveDdlBarViewModel? LiveDdl
    {
        get
        {
            if (_liveDdl is null && _domainStrategy is DdlDomainStrategy)
                _liveDdl = new LiveDdlBarViewModel(this);

            return _liveDdl;
        }
    }

    // â”€â”€ Managers & Controllers â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    // Tracks per-node PropertyChanged handlers so they can be removed when a node is deleted.
    private readonly Dictionary<NodeViewModel, PropertyChangedEventHandler> _nodeValidationHandlers = new();

    private readonly INodeManager _nodeManager;
    private readonly IPinManager _pinManager;
    private readonly ISelectionManager _selectionManager;
    private readonly ILocalizationService _localizationService;
    private readonly NodeLayoutManager _layoutManager;
    private readonly ValidationManager _validationManager;
    private readonly ICanvasDomainStrategy _domainStrategy;
    private readonly IAliasConventionRegistry _aliasConventionRegistry;

    private SubCanvasEditingController _subCanvasController = null!;
    private CanvasAutoJoinController _autoJoinController = null!;

    // â”€â”€ Event handler storage for proper disposal â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    // These fields store references to event handlers so they can be unsubscribed in Dispose()
    private PropertyChangedEventHandler? _liveSqlPropertyChangedHandler;
    private PropertyChangedEventHandler? _selfPropertyChangedHandler;
    private PropertyChangedEventHandler? _layoutManagerPropertyChangedHandler;
    private PropertyChangedEventHandler? _localizationPropertyChangedHandler;
    private PropertyChangedEventHandler? _explainPlanPropertyChangedHandler;
    private NotifyCollectionChangedEventHandler? _nodesCollectionChangedHandler;
    private NotifyCollectionChangedEventHandler? _connectionsCollectionChangedHandler;
    private Action? _propertyPanelNamingChangedHandler;
    private Action? _propertyPanelWireStyleChangedHandler;

    // â”€â”€ Canvas state â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    private string _queryText = "";
    private bool _isDirty;
    private string? _filePath;
    private CanvasWireCurveMode _wireCurveMode = CanvasWireCurveMode.Bezier;
    private DbMetadata? _databaseMetadata;
    private ConnectionConfig? _activeConnectionConfig;
    // Backward-compatibility bridge used by legacy tests that inspect/corrupt
    // sub-editor sessions via reflection on CanvasViewModel.
    private object? _cteEditorSession;
    private object? _viewEditorSession;


    public void SetCanvasContext(CanvasContext context)
    {
        SearchMenu.CanvasContext = context;
        Sidebar.NodesList.CanvasContext = context;
    }

    public string QueryText
    {
        get => _queryText;
        set => Set(ref _queryText, value);
    }

    public bool IsDirty
    {
        get => _isDirty;
        set => Set(ref _isDirty, value);
    }

    public string? CurrentFilePath
    {
        get => _filePath;
        set
        {
            Set(ref _filePath, value);
            RaisePropertyChanged(nameof(WindowTitle));
        }
    }

    public CanvasWireCurveMode WireCurveMode
    {
        get => _wireCurveMode;
        set => Set(ref _wireCurveMode, value);
    }

    /// <summary>
    /// The currently connected database metadata (schemas, tables, columns).
    /// Updated when a new database connection is established.
    /// </summary>
    public DbMetadata? DatabaseMetadata
    {
        get => _databaseMetadata;
        set => Set(ref _databaseMetadata, value);
    }

    /// <summary>
    /// The active database connection configuration.
    /// Used to execute queries for preview results.
    /// </summary>
    public ConnectionConfig? ActiveConnectionConfig
    {
        get => _activeConnectionConfig;
        set
        {
            if (!Set(ref _activeConnectionConfig, value))
                return;

            if (LiveSql is not null)
                LiveSql.Provider = value?.Provider ?? DatabaseProvider.Postgres;
        }
    }

    public string WindowTitle =>
        (
            CurrentFilePath is not null
                ? Path.GetFileNameWithoutExtension(CurrentFilePath)
                : L("main.window.untitled", "Untitled")
        )
        + (IsDirty ? " â€¢" : "")
        + $" - {L("app.windowTitle", "Visual SQL Architect")}";

    public bool IsCanvasEmpty => Nodes.Count == 0;

    /// <summary>
    /// True when canvas should be disabled (e.g., during database connection).
    /// </summary>
    public bool IsCanvasDisabled => ConnectionManager.IsConnecting;
    public bool IsInCteEditor => _subCanvasController.IsInCteEditor;
    public bool IsInViewEditor => _subCanvasController.IsInViewEditor;
    public bool IsCanvasDimmedBySubcanvas => _subCanvasController.IsCanvasDimmedBySubcanvas;
    public string CteEditorBreadcrumb => _subCanvasController.CteEditorBreadcrumb;
    public string EditorExitLabel => _subCanvasController.EditorExitLabel;
    public string EditorExitA11y => _subCanvasController.EditorExitA11y;

    // â”€â”€ Layout properties (delegated to NodeLayoutManager) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    public double Zoom
    {
        get => _layoutManager.Zoom;
        set => _layoutManager.Zoom = value;
    }

    public Point PanOffset
    {
        get => _layoutManager.PanOffset;
        set => _layoutManager.PanOffset = value;
    }

    public bool SnapToGrid
    {
        get => _layoutManager.SnapToGrid;
        set => _layoutManager.SnapToGrid = value;
    }

    public const int GridSize = NodeLayoutManager.GridSize;
    public string ZoomPercent => _layoutManager.ZoomPercent;
    public string SnapToGridLabel => _layoutManager.SnapToGridLabel;

    // â”€â”€ Validation properties (delegated to ValidationManager) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    public bool HasErrors => _validationManager.HasErrors;
    public int ErrorCount => _validationManager.ErrorCount;
    public int WarningCount => _validationManager.WarningCount;
    public bool HasOrphanNodes => _validationManager.HasOrphanNodes;
    public int OrphanCount => _validationManager.OrphanCount;
    public bool HasNamingViolations => _validationManager.HasNamingViolations;
    public int NamingConformance => _validationManager.NamingConformance;

    // â”€â”€ Commands â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    public RelayCommand UndoCommand { get; }
    public RelayCommand RedoCommand { get; }
    public RelayCommand DeleteSelectedCommand { get; }
    public RelayCommand CleanupOrphansCommand { get; }
    public RelayCommand AutoFixNamingCommand { get; }
    public RelayCommand AutoLayoutCommand { get; }
    public RelayCommand BringSelectionToFrontCommand { get; }
    public RelayCommand SendSelectionToBackCommand { get; }
    public RelayCommand BringSelectionForwardCommand { get; }
    public RelayCommand SendSelectionBackwardCommand { get; }
    public RelayCommand NormalizeLayersCommand { get; }
    public RelayCommand EnterCteEditorCommand { get; }
    public RelayCommand ExitCteEditorCommand { get; }
    public RelayCommand DiscardAndExitSubEditorCommand { get; }
    public RelayCommand RunSelectedAutoJoinCommand { get; }

    // â”€ Delegated from SelectionManager
    public RelayCommand SelectAllCommand => _selectionManager.SelectAllCommand;
    public RelayCommand DeselectAllCommand => _selectionManager.DeselectAllCommand;
    public RelayCommand AlignLeftCommand => _selectionManager.AlignLeftCommand;
    public RelayCommand AlignRightCommand => _selectionManager.AlignRightCommand;
    public RelayCommand AlignTopCommand => _selectionManager.AlignTopCommand;
    public RelayCommand AlignBottomCommand => _selectionManager.AlignBottomCommand;
    public RelayCommand AlignCenterHCommand => _selectionManager.AlignCenterHCommand;
    public RelayCommand AlignCenterVCommand => _selectionManager.AlignCenterVCommand;
    public RelayCommand DistributeHCommand => _selectionManager.DistributeHCommand;
    public RelayCommand DistributeVCommand => _selectionManager.DistributeVCommand;

    // â”€ Delegated from NodeLayoutManager
    public RelayCommand ZoomInCommand => _layoutManager.ZoomInCommand;
    public RelayCommand ZoomOutCommand => _layoutManager.ZoomOutCommand;
    public RelayCommand ResetZoomCommand => _layoutManager.ResetZoomCommand;
    public RelayCommand FitToScreenCommand => _layoutManager.FitToScreenCommand;
    public RelayCommand ToggleSnapCommand => _layoutManager.ToggleSnapCommand;

    public bool HasTwoSelectedTableSources => _autoJoinController.HasTwoSelectedTableSources;

    // â”€â”€ Constructor â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    public CanvasViewModel()
        : this(null, null, null, null, null)
    {
    }

    public CanvasViewModel(
        INodeManager? nodeManager,
        IPinManager? pinManager,
        ISelectionManager? selectionManager,
        ILocalizationService? localizationService,
        ICanvasDomainStrategy? domainStrategy = null,
        ToastCenterViewModel? toastCenter = null,
        IAliasConventionRegistry? aliasConventionRegistry = null)
    {
        Toasts = toastCenter ?? new ToastCenterViewModel();
        UndoRedo = new UndoRedoStack(this);
        SearchMenu = new SearchMenuViewModel();
        PropertyPanel = new PropertyPanelViewModel(UndoRedo, () => Connections);
        PropertyPanel.SelectedWireCurveMode = WireCurveMode;
        _localizationService = localizationService ?? LocalizationService.Instance;
        _domainStrategy = domainStrategy ?? new QueryDomainStrategy();
        _aliasConventionRegistry = aliasConventionRegistry
            ?? Validation.Conventions.AliasConventionRegistry.CreateDefault();

        // Initialise managers
        _nodeManager = nodeManager ?? new NodeManager(Nodes, Connections, UndoRedo, PropertyPanel, SearchMenu);
        _selectionManager = selectionManager ?? new SelectionManager(Nodes, PropertyPanel, UndoRedo);
        _layoutManager = new NodeLayoutManager(this, UndoRedo);
        _pinManager = pinManager ?? new PinManager(Nodes, Connections, UndoRedo);
        _validationManager = new ValidationManager(this);

        // Forward layout manager changes through this ViewModel so bindings observing
        // CanvasViewModel receive updates for delegated properties (Zoom/Pan/Snap labels).
        _layoutManagerPropertyChangedHandler = (_, e) =>
        {
            switch (e.PropertyName)
            {
                case nameof(NodeLayoutManager.Zoom):
                    RaisePropertyChanged(nameof(Zoom));
                    RaisePropertyChanged(nameof(ZoomPercent));
                    break;

                case nameof(NodeLayoutManager.PanOffset):
                    RaisePropertyChanged(nameof(PanOffset));
                    break;

                case nameof(NodeLayoutManager.SnapToGrid):
                    RaisePropertyChanged(nameof(SnapToGrid));
                    RaisePropertyChanged(nameof(SnapToGridLabel));
                    break;

                case nameof(NodeLayoutManager.ZoomPercent):
                    RaisePropertyChanged(nameof(ZoomPercent));
                    break;

                case nameof(NodeLayoutManager.SnapToGridLabel):
                    RaisePropertyChanged(nameof(SnapToGridLabel));
                    break;
            }
        };
        _layoutManager.PropertyChanged += _layoutManagerPropertyChangedHandler;

        _localizationPropertyChangedHandler = (_, e) =>
        {
            if (e.PropertyName is "" or "Item[]" or nameof(ILocalizationService.CurrentCulture))
            {
                RaisePropertyChanged(nameof(CteEditorBreadcrumb));
                RaisePropertyChanged(nameof(EditorExitLabel));
                RaisePropertyChanged(nameof(EditorExitA11y));
            }
        };
        _localizationService.PropertyChanged += _localizationPropertyChangedHandler;

        LiveSql = new LiveSqlBarViewModel(this);

        // Create child VMs required by the controllers before building commands
        Diagnostics = new AppDiagnosticsViewModel(this);
        AutoJoin = new AutoJoinOverlayViewModel();
        ManualJoinDialog = new ManualJoinDialogViewModel(_localizationService);

        // Initialise focused controllers (SRP extractions)
        _subCanvasController = new SubCanvasEditingController(
            this,
            _domainStrategy,
            _selectionManager,
            Diagnostics,
            _localizationService,
            notifyStateChanged: NotifySubEditorStateChanged,
            getCteSession: () => _cteEditorSession,
            setCteSession: session => _cteEditorSession = session,
            getViewSession: () => _viewEditorSession,
            setViewSession: session => _viewEditorSession = session
        );
        _autoJoinController = new CanvasAutoJoinController(
            Nodes,
            Connections,
            AutoJoin,
            ManualJoinDialog,
            Toasts,
            _localizationService,
            applicationService: null,
            suggestionService: null,
            notifier: null,
            spawnNode: (def, pos) => _nodeManager.SpawnNode(def, pos),
            connectPins: (from, to) =>
            {
                _pinManager.ConnectPins(from, to);
                IsDirty = true;
            },
            notifyRunSelectedAutoJoinCanExecute: () => RunSelectedAutoJoinCommand?.NotifyCanExecuteChanged()
        );

        // Build commands
        UndoCommand = new RelayCommand(UndoRedo.Undo, () => UndoRedo.CanUndo);
        RedoCommand = new RelayCommand(UndoRedo.Redo, () => UndoRedo.CanRedo);
        DeleteSelectedCommand = new RelayCommand(DeleteSelected);
        CleanupOrphansCommand = new RelayCommand(CleanupOrphans, () => HasOrphanNodes);
        AutoFixNamingCommand = new RelayCommand(AutoFixNaming, () => HasNamingViolations);
        AutoLayoutCommand = new RelayCommand(
            () => _layoutManager.RunAutoLayout(),
            () => Nodes.Count > 0
        );
        BringSelectionToFrontCommand = new RelayCommand(
            () => BringSelectionToFront(),
            () => Nodes.Any(n => n.IsSelected)
        );
        SendSelectionToBackCommand = new RelayCommand(
            () => SendSelectionToBack(),
            () => Nodes.Any(n => n.IsSelected)
        );
        BringSelectionForwardCommand = new RelayCommand(
            () => BringSelectionForward(),
            () => Nodes.Any(n => n.IsSelected)
        );
        SendSelectionBackwardCommand = new RelayCommand(
            () => SendSelectionBackward(),
            () => Nodes.Any(n => n.IsSelected)
        );
        NormalizeLayersCommand = new RelayCommand(
            () => NormalizeLayers(),
            () => Nodes.Count > 0
        );
        EnterCteEditorCommand = new RelayCommand(
            () =>
                _ = _subCanvasController.RunSubEditorActionSafeAsync(
                    _subCanvasController.EnterSelectedCteEditorAsync
                ),
            _subCanvasController.CanEnterSelectedCteEditor
        );
        ExitCteEditorCommand = new RelayCommand(
            () =>
                _ = _subCanvasController.RunSubEditorActionSafeAsync(
                    () => _subCanvasController.ExitCteEditorAsync()
                ),
            () => IsInCteEditor
        );
        DiscardAndExitSubEditorCommand = new RelayCommand(
            () =>
                _ = _subCanvasController.RunSubEditorActionSafeAsync(
                    () => _subCanvasController.ExitCteEditorAsync(forceDiscard: true)
                ),
            () => IsInCteEditor
        );
        RunSelectedAutoJoinCommand = new RelayCommand(
            _autoJoinController.RunSelectedAutoJoin,
            () => HasTwoSelectedTableSources
        );

        // Link commands into validation manager for CanExecute refresh
        _validationManager.CleanupOrphansCommand = CleanupOrphansCommand;
        _validationManager.AutoFixNamingCommand = AutoFixNamingCommand;

        Benchmark = new BenchmarkViewModel(this);
        ExplainPlan = new ExplainPlanViewModel(this);
        SqlImporter = new SqlImporterViewModel(this);
        FlowVersions = new FlowVersionOverlayViewModel(this);
        FileHistory = new FileVersionHistoryViewModel(this);

        _explainPlanPropertyChangedHandler = (_, e) =>
        {
            if (e.PropertyName == nameof(ExplainPlanViewModel.HighlightedTableName))
                ApplyExplainNodeHighlight(ExplainPlan.HighlightedTableName);
        };
        ExplainPlan.PropertyChanged += _explainPlanPropertyChangedHandler;

        // Initialize Sidebar with its three tabs
        var nodesList = new NodesListViewModel(
            spawnNode: (definition, position) =>
            {
                _nodeManager.SpawnNode(definition, position);
            }
        );

        var schemaVM = new SchemaViewModel(
            onAddTableNode: (tableName, columns, table, position) =>
            {
                _ = _domainStrategy.TryHandleSchemaTableInsert(
                    table,
                    position,
                    null,
                    null,
                    () => _nodeManager.SpawnTableNode(tableName, columns, position)
                );
            }
        );

        Sidebar = new SidebarViewModel(nodesList, ConnectionManager, schemaVM, Diagnostics);

        // Subscribe to LiveSql property changes with stored handler
        _liveSqlPropertyChangedHandler = (_, e) =>
        {
            if (e.PropertyName == nameof(LiveSqlBarViewModel.RawSql))
                PropertyPanel.UpdateSqlTrace(LiveSql.RawSql);
        };
        LiveSql.PropertyChanged += _liveSqlPropertyChangedHandler;
        DataPreview.ErrorNotified += OnDataPreviewErrorNotified;
        _propertyPanelNamingChangedHandler = () => _validationManager.ScheduleValidation();
        PropertyPanel.NamingSettingsChanged += _propertyPanelNamingChangedHandler;
        _propertyPanelWireStyleChangedHandler =
            () => WireCurveMode = PropertyPanel.SelectedWireCurveMode;
        PropertyPanel.WireStyleChanged += _propertyPanelWireStyleChangedHandler;

        SearchMenu.LoadTables(NodeManager.DemoCatalog);

        // Enable automatic table loading when database is connected
        ConnectionManager.SearchMenu = SearchMenu;
        ConnectionManager.Canvas = this;

        // Update schema tab when database metadata changes
        // Store handler for proper unsubscribe in Dispose()
        _selfPropertyChangedHandler = (_, e) =>
        {
            if (e.PropertyName == nameof(DatabaseMetadata))
                schemaVM.Metadata = DatabaseMetadata;
        };
        this.PropertyChanged += _selfPropertyChangedHandler;

        // Store collection changed handlers for proper unsubscribe
        _nodesCollectionChangedHandler = (_, e) => HandleNodesCollectionChanged(e);
        Nodes.CollectionChanged += _nodesCollectionChangedHandler;

        _connectionsCollectionChangedHandler = (_, e) => HandleConnectionsCollectionChanged(e);
        Connections.CollectionChanged += _connectionsCollectionChangedHandler;

        NotifyLayerCommandsCanExecuteChanged();
        IsDirty = false;
    }

    private void HandleNodesCollectionChanged(NotifyCollectionChangedEventArgs e)
    {
        IsDirty = true;
        RaisePropertyChanged(nameof(IsCanvasEmpty));

        if (e.NewItems is not null)
        {
            foreach (NodeViewModel node in e.NewItems)
                AttachNodeTracking(node);
        }

        if (e.OldItems is not null)
        {
            foreach (NodeViewModel node in e.OldItems)
                DetachNodeTracking(node);
        }

        _validationManager.ScheduleValidation();
        RaisePropertyChanged(nameof(HasTwoSelectedTableSources));
        RunSelectedAutoJoinCommand.NotifyCanExecuteChanged();
        ApplyExplainNodeHighlight(ExplainPlan.HighlightedTableName);
    }

    private void AttachNodeTracking(NodeViewModel node)
    {
        _domainStrategy.OnNodeAdded(node, Connections);

        PropertyChangedEventHandler handler = OnTrackedNodePropertyChanged;
        node.PropertyChanged += handler;
        _nodeValidationHandlers[node] = handler;
    }

    private void DetachNodeTracking(NodeViewModel node)
    {
        if (!_nodeValidationHandlers.TryGetValue(node, out PropertyChangedEventHandler? handler))
            return;

        node.PropertyChanged -= handler;
        _nodeValidationHandlers.Remove(node);
    }

    private void OnTrackedNodePropertyChanged(object? _, PropertyChangedEventArgs e)
    {
        _validationManager.ScheduleValidation();

        if (e.PropertyName != nameof(NodeViewModel.IsSelected))
            return;

        NotifyLayerCommandsCanExecuteChanged();
        NotifyCteEditorCommandsCanExecuteChanged();
        RaisePropertyChanged(nameof(HasTwoSelectedTableSources));
        RunSelectedAutoJoinCommand.NotifyCanExecuteChanged();
    }

    private void HandleConnectionsCollectionChanged(NotifyCollectionChangedEventArgs e)
    {
        if (IsTransientConnectionDragChange(e))
            return;

        IsDirty = true;
        _validationManager.ScheduleValidation();
        NotifyLayerCommandsCanExecuteChanged();
        NotifyCteEditorCommandsCanExecuteChanged();

        // Single pass â€” check each node type once instead of three Where iterations.
        foreach (NodeViewModel node in Nodes)
        {
            if (node.IsResultOutput)
                node.SyncOutputColumns(Connections);
            else if (node.IsColumnList)
                node.SyncColumnListPins(Connections);
            else if (node.IsLogicGate)
                node.SyncLogicGatePins(Connections);
            else if (node.IsWindowFunction)
                node.SyncWindowFunctionPins(Connections);
        }

        if (e.NewItems is not null)
        {
            foreach (ConnectionViewModel connection in e.NewItems.Cast<object>().OfType<ConnectionViewModel>())
                _domainStrategy.OnConnectionEstablished(connection, Connections, Nodes);
        }

        if (e.OldItems is not null)
        {
            foreach (ConnectionViewModel connection in e.OldItems.Cast<object>().OfType<ConnectionViewModel>())
                _domainStrategy.OnConnectionRemoved(connection, Connections, Nodes);
        }

        PropertyPanel.RefreshConnectionOverrides();
    }

    private static bool IsTransientConnectionDragChange(NotifyCollectionChangedEventArgs e)
    {
        static bool HasStructuralEndpoint(NotifyCollectionChangedEventArgs args)
        {
            bool NewHasStructured = args.NewItems is not null
                && args.NewItems.Cast<object>().OfType<ConnectionViewModel>().Any(c => c.ToPin is not null);
            bool OldHasStructured = args.OldItems is not null
                && args.OldItems.Cast<object>().OfType<ConnectionViewModel>().Any(c => c.ToPin is not null);
            return NewHasStructured || OldHasStructured;
        }

        return !HasStructuralEndpoint(e);
    }

    private void NotifyLayerCommandsCanExecuteChanged()
    {
        BringSelectionToFrontCommand.NotifyCanExecuteChanged();
        SendSelectionToBackCommand.NotifyCanExecuteChanged();
        BringSelectionForwardCommand.NotifyCanExecuteChanged();
        SendSelectionBackwardCommand.NotifyCanExecuteChanged();
        NormalizeLayersCommand.NotifyCanExecuteChanged();
    }

    private void NotifyCteEditorCommandsCanExecuteChanged()
    {
        EnterCteEditorCommand.NotifyCanExecuteChanged();
        ExitCteEditorCommand.NotifyCanExecuteChanged();
        DiscardAndExitSubEditorCommand.NotifyCanExecuteChanged();
    }

    // â”€â”€ Sub-editor operations (delegated to SubCanvasEditingController) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    public Task<bool> EnterSelectedCteEditorAsync() =>
        _subCanvasController.EnterSelectedCteEditorAsync();

    public Task<bool> EnterCteEditorAsync(NodeViewModel cteNode) =>
        _subCanvasController.EnterCteEditorAsync(cteNode);

    public Task<bool> ExitCteEditorAsync(bool forceDiscard = false) =>
        _subCanvasController.ExitCteEditorAsync(forceDiscard);

    public Task<bool> EnterViewEditorAsync(NodeViewModel viewNode) =>
        _subCanvasController.EnterViewEditorAsync(viewNode);

    private void NotifySubEditorStateChanged()
    {
        RaisePropertyChanged(nameof(IsInCteEditor));
        RaisePropertyChanged(nameof(IsInViewEditor));
        RaisePropertyChanged(nameof(IsCanvasDimmedBySubcanvas));
        RaisePropertyChanged(nameof(CteEditorBreadcrumb));
        RaisePropertyChanged(nameof(EditorExitLabel));
        RaisePropertyChanged(nameof(EditorExitA11y));
        NotifyCteEditorCommandsCanExecuteChanged();
    }

    // â”€â”€ Node operations (delegated to NodeManager) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    public NodeViewModel SpawnNode(NodeDefinition def, Point pos) =>
        _nodeManager.SpawnNode(def, pos);

    public NodeViewModel SpawnTableNode(
        string table,
        IEnumerable<(string n, PinDataType t)> cols,
        Point pos
    ) => _nodeManager.SpawnTableNode(table, cols, pos);

    public void DeleteSelected() => _nodeManager.DeleteSelected();

    public void CleanupOrphans() => _nodeManager.CleanupOrphans();

    /// <summary>
    /// Initializes canvas with demo nodes. Used for unit tests and initial UI setup.
    /// </summary>
    public void InitializeDemoNodes() => _nodeManager.SpawnDemoNodes(UndoRedo);

    // â”€â”€ Snippets â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    /// <summary>
    /// Saves the currently selected nodes (and their internal connections) as a
    /// named snippet in the persistent snippet store.
    /// </summary>
    public void SaveSelectionAsSnippet(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return;
        List<NodeViewModel> selected = _selectionManager.SelectedNodes();
        if (selected.Count == 0)
            return;

        (List<SavedNode> nodes, List<SavedConnection> conns) =
            CanvasSerializer.SerialiseSubgraph(selected, Connections);

        var snippet = new SavedSnippet(
            Id: Guid.NewGuid().ToString(),
            Name: name.Trim(),
            Tags: null,
            Description: null,
            CreatedAt: DateTime.UtcNow.ToString("o"),
            Nodes: nodes,
            Connections: conns
        );

        SnippetStore.Add(snippet);
        SearchMenu.LoadSnippets();
    }

    /// <summary>
    /// Inserts a saved snippet into the canvas, centered at <paramref name="canvasPos"/>,
    /// with fresh node IDs to avoid conflicts with the existing graph.
    /// </summary>
    public void InsertSnippet(SavedSnippet snippet, Point canvasPos) =>
        CanvasSerializer.InsertSubgraph(snippet.Nodes, snippet.Connections, this, canvasPos);

    /// <summary>
    /// Loads a query template by clearing the canvas and invoking the template's Build action.
    /// Resets undo history and dirty flag so the user starts with a clean slate.
    /// </summary>
    public void LoadTemplate(QueryTemplate template)
    {
        var stateBeforeTemplate = new RestoreCanvasStateCommand(this, "Load Template");

        Connections.Clear();
        Nodes.Clear();
        CurrentFilePath = null;
        QueryText = "";
        Zoom = 1.0;
        PanOffset = new Point(0, 0);
        template.Build(this);
        IsDirty = false;

        stateBeforeTemplate.CaptureAfterState(this);
        UndoRedo.Execute(stateBeforeTemplate);
    }

    /// <summary>
    /// Resets the canvas to a clean state and sets the database metadata.
    /// Called when a user connects to a new database.
    /// </summary>
    public void SetDatabaseAndResetCanvas(DbMetadata? metadata)
    {
        SetDatabaseAndResetCanvas(metadata, null);
    }

    /// <summary>
    /// Updates the database metadata and connection configuration without clearing the canvas.
    /// </summary>
    public void SetDatabaseContext(DbMetadata? metadata, ConnectionConfig? config)
    {
        DatabaseMetadata = metadata;
        ActiveConnectionConfig = config;
    }

    /// <summary>
    /// Resets the canvas to a clean state and sets the database metadata and connection config.
    /// Called when a user connects to a new database.
    /// </summary>
    public void SetDatabaseAndResetCanvas(DbMetadata? metadata, ConnectionConfig? config)
    {
        // Clear the current canvas
        Connections.Clear();
        Nodes.Clear();
        CurrentFilePath = null;
        QueryText = "";
        Zoom = 1.0;
        PanOffset = new Point(0, 0);
        IsDirty = false;
        UndoRedo.Clear();

        // Set the new database metadata and connection config
        DatabaseMetadata = metadata;
        ActiveConnectionConfig = config;

        // Reload demo nodes if no metadata provided
        if (metadata is null)
            _nodeManager.SpawnDemoNodes(UndoRedo);
    }

    // â”€â”€ Naming & layout â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    /// <summary>Converts all alias violations to snake_case (undoable).</summary>
    public void AutoFixNaming()
    {
        NamingConventionPolicy policy = PropertyPanel.BuildNamingConventionPolicy();
        var cmd = new AutoFixNamingCommand(Nodes, policy, _aliasConventionRegistry);
        if (!cmd.HasChanges)
            return;
        UndoRedo.Execute(cmd);
    }

    public IAliasConventionRegistry AliasConventions => _aliasConventionRegistry;

    /// <summary>Arranges nodes into logical columns (undoable). Pass a scope to layout only selected nodes.</summary>
    public void RunAutoLayout(IReadOnlyList<NodeViewModel>? scope = null)
    {
        if (Nodes.Count == 0)
            return;
        UndoRedo.Execute(new AutoLayoutCommand(this, scope));
    }

    // â”€â”€ Pin & connection operations (delegated to PinManager) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    public void ConnectPins(PinViewModel from, PinViewModel to)
    {
        _pinManager.ConnectPins(from, to);
        IsDirty = true;
    }

    public void DeleteConnection(ConnectionViewModel conn)
    {
        _pinManager.DeleteConnection(conn);
        IsDirty = true;
    }

    /// <summary>
    /// Propagates a parameter change to the active domain strategy so that
    /// dependent nodes (e.g., table previews) can re-sync when a connected
    /// type-defining node's parameter changes.
    /// </summary>
    internal void NotifyNodeParameterChanged(NodeViewModel node, string paramName)
        => _domainStrategy.OnParameterChanged(node, paramName, Connections, Nodes);

    internal void ClearNarrowingIfNeeded(IEnumerable<NodeViewModel> nodes) =>
        _pinManager.ClearNarrowingIfNeeded(nodes);

    // â”€â”€ Selection (delegated to SelectionManager) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    public void SelectAll() => _selectionManager.SelectAll();

    public void DeselectAll() => _selectionManager.DeselectAll();

    public void SelectNode(NodeViewModel node, bool add = false) =>
        _selectionManager.SelectNode(node, add);

    public bool FocusNodeById(string? nodeId)
    {
        if (string.IsNullOrWhiteSpace(nodeId))
            return false;

        NodeViewModel? node = Nodes.FirstOrDefault(n =>
            n.Id.Equals(nodeId, StringComparison.Ordinal)
        );
        if (node is null)
            return false;

        _selectionManager.SelectNode(node, add: false);
        _layoutManager.FocusOnNode(node);
        return true;
    }

    public bool BringSelectionToFront() =>
        ApplyLayerOrder(
            L("main.layerOrder.bringToFront", "Bring to front"),
            NodeLayerOrdering.BringToFront,
            requireSelection: true
        );

    public bool SendSelectionToBack() =>
        ApplyLayerOrder(
            L("main.layerOrder.sendToBack", "Send to back"),
            NodeLayerOrdering.SendToBack,
            requireSelection: true
        );

    public bool BringSelectionForward() =>
        ApplyLayerOrder(
            L("main.layerOrder.bringForward", "Bring forward"),
            NodeLayerOrdering.BringForward,
            requireSelection: true
        );

    public bool SendSelectionBackward() =>
        ApplyLayerOrder(
            L("main.layerOrder.sendBackward", "Send backward"),
            NodeLayerOrdering.SendBackward,
            requireSelection: true
        );

    public bool NormalizeLayers() =>
        ApplyLayerOrder(
            L("main.layerOrder.normalizeLayers", "Normalize layers"),
            NodeLayerOrdering.OrderByZ,
            requireSelection: false
        );

    private bool ApplyLayerOrder(
        string action,
        Func<List<NodeViewModel>, List<NodeViewModel>> reorder,
        bool requireSelection
    )
    {
        List<NodeViewModel> all = Nodes.ToList();
        if (all.Count == 0)
            return false;
        if (requireSelection && all.All(n => !n.IsSelected))
            return false;

        Dictionary<NodeViewModel, int> from = all.ToDictionary(n => n, n => n.ZOrder);
        List<NodeViewModel> ordered = reorder(all);
        Dictionary<NodeViewModel, int> to = NodeLayerOrdering.BuildNormalizedMap(ordered);

        if (from.All(kv => to.TryGetValue(kv.Key, out int z) && z == kv.Value))
            return false;

        UndoRedo.Execute(new ReorderNodesCommand(action, from, to));
        return true;
    }

    // â”€â”€ Coordinate transforms â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    public void ZoomToward(Point screen, double factor)
    {
        double old = Zoom;
        Zoom = Math.Clamp(old * factor, 0.15, 4.0);
        PanOffset = new Point(
            screen.X - (screen.X - PanOffset.X) * (Zoom / old),
            screen.Y - (screen.Y - PanOffset.Y) * (Zoom / old)
        );
    }

    /// <summary>
    /// Informs the layout manager of the current viewport size so that
    /// <c>FitToScreen</c> can compute an accurate zoom level and pan offset.
    /// Called by <c>InfiniteCanvas.ArrangeOverride</c> after each layout pass.
    /// </summary>
    public void SetViewportSize(double width, double height) =>
        _layoutManager.SetViewportSize(width, height);

    public Point ScreenToCanvas(Point s) =>
        new((s.X - PanOffset.X) / Zoom, (s.Y - PanOffset.Y) / Zoom);

    public Point CanvasToScreen(Point c) =>
        new(c.X * Zoom + PanOffset.X, c.Y * Zoom + PanOffset.Y);

    // â”€â”€ Query & export â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    public void UpdateQueryText(string sql)
    {
        QueryText = sql;
        DataPreview.QueryText = sql;
    }

    /// <summary>
    /// Finds the first export node of <paramref name="exportType"/> and triggers export.
    /// Returns the generated file path, or null if none found or export failed.
    /// </summary>
    public async Task<string?> TriggerExportAsync(NodeType exportType, string? overridePath = null)
    {
        NodeViewModel? node = Nodes.FirstOrDefault(n => n.Type == exportType);
        if (node is null)
            return null;
        return await ExportNodeHandler.RunExportAsync(this, node, overridePath);
    }

    public void ScheduleValidation() => _validationManager.ScheduleValidation();

    public static double Snap(double v) => NodeLayoutManager.Snap(v);

    /// <summary>Forwards to <see cref="NodeManager.DemoCatalog"/> â€” kept for backwards compatibility.</summary>
    public static IReadOnlyList<(
        string FullName,
        IReadOnlyList<(string Name, PinDataType Type)> Cols
    )> DemoCatalog => NodeManager.DemoCatalog;

    // â”€â”€ Auto-join (delegated to CanvasAutoJoinController) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    public void TriggerAutoJoinAnalysis(string newTableFullName) =>
        _autoJoinController.TriggerAutoJoinAnalysis(newTableFullName);

    public void AnalyzeAllCanvasJoins() =>
        _autoJoinController.AnalyzeAllCanvasJoins();

    public void NotifySuccess(string message, string? details = null) =>
        Toasts.ShowSuccess(message, details);

    public void NotifyWarning(string message, string? details = null) =>
        Toasts.ShowWarning(message, details);

    public void NotifyWarning(string message, string? details, Action onDetails) =>
        Toasts.ShowWarning(message, details, onDetails);

    public void NotifyError(string message, string? details = null) =>
        Toasts.ShowError(message, details);

    private void OnDataPreviewErrorNotified(string message, string? details) =>
        NotifyError(message, details);

    private string L(string key, string fallback)
    {
        string value = _localizationService[key];
        return string.Equals(value, key, StringComparison.Ordinal) ? fallback : value;
    }

    private void ApplyExplainNodeHighlight(string? highlightedTableName) =>
        CanvasTableHighlightEngine.ApplyHighlight(Nodes, highlightedTableName);

    /// <summary>
    /// Disposes all resources and unsubscribes from all event handlers.
    /// Called when the CanvasViewModel is being replaced (e.g., Ctrl+N to create new canvas).
    /// Prevents memory leaks by releasing references to event handlers and collection handlers.
    /// </summary>
    public void Dispose()
    {
        // Dispose AutoJoin overlay (which clears its cards and handlers)
        AutoJoin?.Dispose();

        // Unsubscribe from LiveSql PropertyChanged
        if (_liveSqlPropertyChangedHandler is not null)
            LiveSql.PropertyChanged -= _liveSqlPropertyChangedHandler;

        DataPreview.ErrorNotified -= OnDataPreviewErrorNotified;
        if (_propertyPanelNamingChangedHandler is not null)
            PropertyPanel.NamingSettingsChanged -= _propertyPanelNamingChangedHandler;
        if (_propertyPanelWireStyleChangedHandler is not null)
            PropertyPanel.WireStyleChanged -= _propertyPanelWireStyleChangedHandler;

        // Unsubscribe from self PropertyChanged
        if (_selfPropertyChangedHandler is not null)
            this.PropertyChanged -= _selfPropertyChangedHandler;

        // Unsubscribe from layout manager PropertyChanged forwarding
        if (_layoutManagerPropertyChangedHandler is not null)
            _layoutManager.PropertyChanged -= _layoutManagerPropertyChangedHandler;

        if (_localizationPropertyChangedHandler is not null)
            _localizationService.PropertyChanged -= _localizationPropertyChangedHandler;
        if (_explainPlanPropertyChangedHandler is not null)
            ExplainPlan.PropertyChanged -= _explainPlanPropertyChangedHandler;

        _autoJoinController?.Dispose();

        // Unsubscribe from collection changed handlers
        if (_nodesCollectionChangedHandler is not null)
            Nodes.CollectionChanged -= _nodesCollectionChangedHandler;

        if (_connectionsCollectionChangedHandler is not null)
            Connections.CollectionChanged -= _connectionsCollectionChangedHandler;

        // Unsubscribe from all node validation handlers
        foreach (var handler in _nodeValidationHandlers.Values)
            foreach (var node in Nodes)
                node.PropertyChanged -= handler;
        _nodeValidationHandlers.Clear();
    }

    /// <summary>
    /// Replaces the entire canvas graph with new nodes and connections.
    /// </summary>
    public void ReplaceGraph(IEnumerable<NodeViewModel> nodes, IEnumerable<ConnectionViewModel> connections)
    {
        Nodes.Clear();
        Connections.Clear();

        foreach (NodeViewModel node in nodes)
            Nodes.Add(node);

        foreach (ConnectionViewModel connection in connections)
            Connections.Add(connection);
    }

    /// <summary>
    /// Gets or sets the database provider for DDL generation and compilation.
    /// </summary>
    public DatabaseProvider Provider
    {
        get => LiveDdl?.Provider ?? DatabaseProvider.SqlServer;
        set
        {
            if (LiveDdl is not null)
                LiveDdl.Provider = value;
        }
    }
}

