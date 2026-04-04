using System.Collections.ObjectModel;
using System.Windows.Input;
using Avalonia;
using VisualSqlArchitect.Nodes;
using VisualSqlArchitect.UI.ViewModels.Canvas;

namespace VisualSqlArchitect.UI.ViewModels;

/// <summary>
/// ViewModel for the Nodes list tab in the sidebar.
/// Displays all available node types grouped by category, ready to spawn on canvas.
/// </summary>
public sealed class NodesListViewModel : ViewModelBase
{
    private string _searchQuery = string.Empty;
    private CanvasContext _canvasContext = CanvasContext.Query;

    /// <summary>
    /// The search query to filter node types.
    /// </summary>
    public string SearchQuery
    {
        get => _searchQuery;
        set
        {
            if (Set(ref _searchQuery, value))
            {
                InitializeGroups(_spawnNode);
                RaisePropertyChanged(nameof(ShowIntro));
                RaisePropertyChanged(nameof(HasResults));
            }
        }
    }

    public CanvasContext CanvasContext
    {
        get => _canvasContext;
        set
        {
            if (!Set(ref _canvasContext, value))
                return;

            // Avoid residual results from another mode context.
            _searchQuery = string.Empty;
            RaisePropertyChanged(nameof(SearchQuery));
            InitializeGroups(_spawnNode);
            RaisePropertyChanged(nameof(ShowIntro));
            RaisePropertyChanged(nameof(HasResults));
        }
    }

    /// <summary>
    /// Whether to show the intro message.
    /// </summary>
    public bool ShowIntro => string.IsNullOrEmpty(SearchQuery);

    /// <summary>
    /// Command to clear the search query.
    /// </summary>
    public ICommand ClearSearchCommand { get; }

    /// <summary>
    /// Filtered and grouped node types, ready for display.
    /// </summary>
    public ObservableCollection<NodeTypeGroupViewModel> FilteredGroups { get; } = new();
    public bool HasResults => FilteredGroups.Count > 0;

    private readonly Action<NodeDefinition, Point> _spawnNode;

    public NodesListViewModel(Action<NodeDefinition, Point> spawnNode)
    {
        _spawnNode = spawnNode;
        ClearSearchCommand = new RelayCommand(() => SearchQuery = "");

        // Build initial groups
        InitializeGroups(_spawnNode);
    }

    private void InitializeGroups(Action<NodeDefinition, Point> spawnNode)
    {
        FilteredGroups.Clear();

        if (CanvasContext == CanvasContext.Ddl)
        {
            InitializeDdlGroups(spawnNode);
            RaisePropertyChanged(nameof(HasResults));
            return;
        }

        // Create a dictionary to hold groups as we build them
        var groupsDict = new Dictionary<NodeCategory, NodeTypeGroupViewModel>();

        // Get all node definitions and group by category
        foreach (var def in NodeDefinitionRegistry.All)
        {
            if (!IsDefinitionAllowedInContext(def))
                continue;

            // Filter based on search
            if (!string.IsNullOrEmpty(SearchQuery))
            {
                var query = SearchQuery.ToLowerInvariant();
                if (!def.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase) &&
                    !def.Description.Contains(query, StringComparison.OrdinalIgnoreCase))
                    continue;
            }

            // Ensure group exists for this category
            if (!groupsDict.TryGetValue(def.Category, out var group))
            {
                var color = GetCategoryColor(def.Category);
                group = new NodeTypeGroupViewModel(def.Category, color);
                groupsDict[def.Category] = group;
            }

            // Add item to group
            var itemVm = new NodeTypeItemViewModel(
                def,
                group.Color,
                defn => spawnNode(defn, new Point(200, 200))
            );
            group.Items.Add(itemVm);
            group.Count = group.Items.Count;
        }

        // Add groups to FilteredGroups in order
        foreach (var cat in Enum.GetValues(typeof(NodeCategory)).Cast<NodeCategory>().OrderBy(c => (int)c))
        {
            if (groupsDict.TryGetValue(cat, out var group) && group.Items.Count > 0)
            {
                FilteredGroups.Add(group);
            }
        }

        RaisePropertyChanged(nameof(HasResults));
    }

    private void InitializeDdlGroups(Action<NodeDefinition, Point> spawnNode)
    {
        var groups = new Dictionary<string, NodeTypeGroupViewModel>(StringComparer.OrdinalIgnoreCase);
        var groupOrder = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (NodeDefinition def in NodeDefinitionRegistry.All)
        {
            if (!IsDefinitionAllowedInContext(def))
                continue;

            if (!MatchesSearch(def))
                continue;

            (string key, string name, string color, int order) = ClassifyDdlGroup(def.Type);

            if (!groups.TryGetValue(key, out NodeTypeGroupViewModel? group))
            {
                group = new NodeTypeGroupViewModel(NodeCategory.Ddl, color, name);
                groups[key] = group;
                groupOrder[key] = order;
            }

            group.Items.Add(
                new NodeTypeItemViewModel(def, color, defn => spawnNode(defn, new Point(200, 200)))
            );
            group.Count = group.Items.Count;
        }

        foreach ((string key, NodeTypeGroupViewModel group) in groups
                     .OrderBy(kv => groupOrder.TryGetValue(kv.Key, out int order) ? order : int.MaxValue)
                     .ThenBy(kv => kv.Value.Name, StringComparer.OrdinalIgnoreCase))
        {
            _ = key;
            FilteredGroups.Add(group);
        }
    }

    private bool MatchesSearch(NodeDefinition def)
    {
        if (string.IsNullOrEmpty(SearchQuery))
            return true;

        string query = SearchQuery.ToLowerInvariant();
        return def.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase)
            || def.Description.Contains(query, StringComparison.OrdinalIgnoreCase);
    }

    private static (string Key, string Name, string Color, int Order) ClassifyDdlGroup(NodeType type)
    {
        return type switch
        {
            NodeType.TableDefinition
                or NodeType.ColumnDefinition
                or NodeType.ViewDefinition
                or NodeType.EnumTypeDefinition
                or NodeType.SequenceDefinition
                or NodeType.ScalarTypeDefinition
                => ("ddl.definitions", "Definitions", "#60A5FA", 0),

            NodeType.PrimaryKeyConstraint
                or NodeType.ForeignKeyConstraint
                or NodeType.UniqueConstraint
                or NodeType.CheckConstraint
                or NodeType.DefaultConstraint
                or NodeType.IndexDefinition
                => ("ddl.constraints", "Constraints and Indexes", "#A78BFA", 1),

            NodeType.CreateTableOutput
                or NodeType.CreateTypeOutput
                or NodeType.CreateSequenceOutput
                or NodeType.CreateTableAsOutput
                or NodeType.CreateViewOutput
                or NodeType.AlterViewOutput
                or NodeType.AlterTableOutput
                or NodeType.CreateIndexOutput
                => ("ddl.outputs", "Outputs", "#22C55E", 2),

            NodeType.AddColumnOp
                or NodeType.DropColumnOp
                or NodeType.RenameColumnOp
                or NodeType.RenameTableOp
                or NodeType.DropTableOp
                or NodeType.AlterColumnTypeOp
                => ("ddl.alter", "Alter Operations", "#F59E0B", 3),

            _ => ("ddl.misc", "Other DDL", "#94A3B8", 4),
        };
    }

    private static string GetCategoryColor(NodeCategory cat) => cat switch
    {
        NodeCategory.DataSource => "#60A5FA",        // Blue
        NodeCategory.StringTransform => "#34D399",   // Emerald
        NodeCategory.MathTransform => "#FBBF24",     // Amber
        NodeCategory.TypeCast => "#A78BFA",          // Violet
        NodeCategory.Comparison => "#F87171",        // Red
        NodeCategory.LogicGate => "#C084FC",         // Purple
        NodeCategory.Json => "#FB923C",              // Orange
        NodeCategory.Aggregate => "#14B8A6",         // Teal
        NodeCategory.Conditional => "#22D3EE",       // Cyan
        NodeCategory.ResultModifier => "#EC4899",    // Pink
        NodeCategory.Output => "#10B981",            // Green
        NodeCategory.Literal => "#6B7280",           // Gray
        _ => "#D1D5DB"
    };

    private bool IsDefinitionAllowedInContext(NodeDefinition def)
    {
        return CanvasContext switch
        {
            CanvasContext.Ddl => def.Category == NodeCategory.Ddl,
            CanvasContext.Query or CanvasContext.ViewSubcanvas => def.Category != NodeCategory.Ddl,
            _ => true,
        };
    }
}
