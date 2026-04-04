namespace VisualSqlArchitect.UI.ViewModels;

/// <summary>
/// ViewModel for the left sidebar containing tabs for nodes, connections,
/// schema, and diagnostics.
/// </summary>
public sealed class SidebarViewModel : ViewModelBase
{
    private ESidebarTab _activeTab = ESidebarTab.Nodes;

    public RelayCommand SelectNodesCommand { get; }
    public RelayCommand SelectConnectionCommand { get; }
    public RelayCommand SelectSchemaCommand { get; }
    public RelayCommand AddNodeCommand { get; }
    public RelayCommand AddConnectionCommand { get; }
    public RelayCommand TogglePreviewCommand { get; }

    public event Action? AddNodeRequested;
    public event Action? AddConnectionRequested;
    public event Action? TogglePreviewRequested;

    /// <summary>
    /// Gets or sets the currently active tab.
    /// </summary>
    public ESidebarTab ActiveTab
    {
        get => _activeTab;
        set
        {
            if (Set(ref _activeTab, value))
            {
                RaisePropertyChanged(nameof(ShowNodes));
                RaisePropertyChanged(nameof(ShowConnection));
                RaisePropertyChanged(nameof(ShowSchema));
            }
        }
    }

    /// <summary>
    /// Returns true when Nodes tab is active.
    /// </summary>
    public bool ShowNodes => ActiveTab == ESidebarTab.Nodes;

    /// <summary>
    /// Returns true when Connection tab is active.
    /// </summary>
    public bool ShowConnection => ActiveTab == ESidebarTab.Connection;

    /// <summary>
    /// Returns true when Schema tab is active.
    /// </summary>
    public bool ShowSchema => ActiveTab == ESidebarTab.Schema;

    /// <summary>
    /// ViewModel for the Nodes list tab.
    /// </summary>
    public NodesListViewModel NodesList { get; }

    /// <summary>
    /// ViewModel for the Connection status tab.
    /// </summary>
    public ConnectionManagerViewModel ConnectionManager { get; }

    /// <summary>
    /// ViewModel for the Schema browser tab.
    /// </summary>
    public SchemaViewModel Schema { get; }

    /// <summary>
    /// ViewModel for diagnostics tab.
    /// </summary>
    public AppDiagnosticsViewModel Diagnostics { get; }

    public SidebarViewModel(
        NodesListViewModel nodesList,
        ConnectionManagerViewModel connectionManager,
        SchemaViewModel schema,
        AppDiagnosticsViewModel diagnostics)
    {
        NodesList = nodesList;
        ConnectionManager = connectionManager;
        Schema = schema;
        Diagnostics = diagnostics;

        SelectNodesCommand = new RelayCommand(() => ActiveTab = ESidebarTab.Nodes);
        SelectConnectionCommand = new RelayCommand(() => ActiveTab = ESidebarTab.Connection);
        SelectSchemaCommand = new RelayCommand(() => ActiveTab = ESidebarTab.Schema);
        AddNodeCommand = new RelayCommand(RequestAddNode);
        AddConnectionCommand = new RelayCommand(RequestAddConnection);
        TogglePreviewCommand = new RelayCommand(() => TogglePreviewRequested?.Invoke());
    }

    private void RequestAddNode()
    {
        ActiveTab = ESidebarTab.Nodes;
        AddNodeRequested?.Invoke();
    }

    private void RequestAddConnection()
    {
        ActiveTab = ESidebarTab.Connection;
        AddConnectionRequested?.Invoke();
    }
}
