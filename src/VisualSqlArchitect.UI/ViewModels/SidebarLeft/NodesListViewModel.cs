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

        // Create a dictionary to hold groups as we build them
        var groupsDict = new Dictionary<NodeCategory, NodeTypeGroupViewModel>();

        // Get all node definitions and group by category
        foreach (var def in NodeDefinitionRegistry.All)
        {
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
}
