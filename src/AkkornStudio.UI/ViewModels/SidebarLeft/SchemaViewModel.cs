using System.Collections.ObjectModel;
using System.Windows.Input;
using Avalonia;
using AkkornStudio.Metadata;
using AkkornStudio.Nodes;
using AkkornStudio.UI.Services.Search;
using AkkornStudio.UI.Services.Theming;

namespace AkkornStudio.UI.ViewModels;

/// <summary>
/// ViewModel for the Schema browser tab in the sidebar.
/// Displays database schema: Tables, Views, Procedures, and Triggers.
/// </summary>
public sealed class SchemaViewModel : ViewModelBase
{
    private sealed record SchemaCatalogEntry(
        SchemaObjectViewModel Item,
        bool IsView,
        string SearchText);

    private static readonly TextSearchService TextSearch = new();
    private string _filterQuery = string.Empty;
    private string? _selectedSchema;
    private bool _isLoading;
    private bool _hasConnection;
    private DbMetadata? _metadata;
    private int _visibleObjectCount;
    private IReadOnlyList<SchemaCatalogEntry> _catalogEntries = [];

    /// <summary>
    /// The name of the currently connected database.
    /// </summary>
    public string DatabaseName { get; private set; } = "No Connection";

    /// <summary>
    /// The search/filter query to narrow down tables and columns.
    /// </summary>
    public string FilterQuery
    {
        get => _filterQuery;
        set
        {
            string normalized = value?.Trim() ?? string.Empty;
            if (!Set(ref _filterQuery, normalized))
                return;

            ApplyFilterToCategories();
            RaisePropertyChanged(nameof(HasFilter));
        }
    }

    /// <summary>
    /// The currently selected schema filter.
    /// </summary>
    public string? SelectedSchema
    {
        get => _selectedSchema;
        set
        {
            if (!Set(ref _selectedSchema, value))
                return;

            IsLoading = true;
            try
            {
                RebuildCatalogEntries();
                ApplyFilterToCategories();
            }
            finally
            {
                IsLoading = false;
            }
        }
    }

    /// <summary>
    /// True when schema is being loaded from the database.
    /// </summary>
    public bool IsLoading
    {
        get => _isLoading;
        set
        {
            if (!Set(ref _isLoading, value))
                return;

            RaisePropertyChanged(nameof(ShowLoadingState));
            RaisePropertyChanged(nameof(ShowFilterEmptyState));
            RaisePropertyChanged(nameof(ShowNoTablesState));
        }
    }

    /// <summary>
    /// True when there is an active database connection.
    /// </summary>
    public bool HasConnection
    {
        get => _hasConnection;
        set
        {
            if (Set(ref _hasConnection, value))
            {
                RaisePropertyChanged(nameof(ShowNoConnectionState));
                RaisePropertyChanged(nameof(ShowLoadingState));
                RaisePropertyChanged(nameof(ShowFilterEmptyState));
                RaisePropertyChanged(nameof(ShowNoTablesState));
            }
        }
    }

    public bool HasFilter => !string.IsNullOrWhiteSpace(FilterQuery);
    public bool ShowNoConnectionState => !HasConnection;
    public bool ShowLoadingState => HasConnection && IsLoading;
    public bool ShowFilterEmptyState => HasConnection && !IsLoading && HasFilter && _visibleObjectCount == 0;
    public bool ShowNoTablesState => HasConnection && !IsLoading && !HasFilter && _visibleObjectCount == 0;

    /// <summary>
    /// The database metadata to display.
    /// </summary>
    public DbMetadata? Metadata
    {
        get => _metadata;
        set
        {
            if (Set(ref _metadata, value))
            {
                DatabaseName = value?.DatabaseName ?? "No Connection";
                if (value is null)
                {
                    _selectedSchema = null;
                    RaisePropertyChanged(nameof(SelectedSchema));
                }
                else if (!string.IsNullOrWhiteSpace(_selectedSchema)
                    && !value.Schemas.Any(schema => string.Equals(schema.Name, _selectedSchema, StringComparison.OrdinalIgnoreCase)))
                {
                    _selectedSchema = value.Schemas.FirstOrDefault()?.Name;
                    RaisePropertyChanged(nameof(SelectedSchema));
                }

                HasConnection = value is not null;
                IsLoading = true;
                try
                {
                    RebuildCatalogEntries();
                    ApplyFilterToCategories();
                }
                finally
                {
                    IsLoading = false;
                }
            }
        }
    }

    /// <summary>
    /// Categorized schema items (Tables, Views, Procedures, Triggers).
    /// </summary>
    public ObservableCollection<SchemaCategoryViewModel> Categories { get; } = new();

    private readonly Action<string, IEnumerable<(string name, PinDataType type)>, TableMetadata, Point>? _onAddTableNode;

    public SchemaViewModel(
        Action<string, IEnumerable<(string name, PinDataType type)>, TableMetadata, Point>? onAddTableNode = null)
    {
        _onAddTableNode = onAddTableNode;
    }

    private void RebuildCatalogEntries()
    {
        _catalogEntries = [];

        if (Metadata is null || Metadata.Schemas.Count == 0)
            return;

        var catalog = new List<SchemaCatalogEntry>();

        IEnumerable<SchemaMetadata> schemas = Metadata.Schemas;
        if (!string.IsNullOrWhiteSpace(SelectedSchema))
        {
            schemas = schemas.Where(schema =>
                string.Equals(schema.Name, SelectedSchema, StringComparison.OrdinalIgnoreCase));
        }

        foreach (var schema in schemas)
        {
            foreach (var table in schema.Tables)
            {
                var fullName = $"{table.Schema ?? "public"}.{table.Name}";

                ICommand? addNodeCmd = null;
                if (_onAddTableNode is not null)
                {
                    var columns = table.Columns.Select(c =>
                    {
                        var pinType = MapSqlTypeToPinDataType(c.DataType);
                        return (c.Name, pinType);
                    });

                    addNodeCmd = new RelayCommand(() =>
                        _onAddTableNode(fullName, columns, table, new Point(200, 200))
                    );
                }

                var item = new SchemaObjectViewModel(
                    table.Name,
                    table.Kind != TableKind.Table ? "Eye" : "Table",
                    fullName,
                    null,
                    null,
                    table,
                    addNodeCmd);

                foreach (var column in table.Columns)
                {
                    string colIcon;
                    string badgeColor;

                    if (column.IsPrimaryKey)
                    {
                        colIcon = "KeyPlus";
                        badgeColor = UiColorConstants.C_FCD34D; // Yellow
                    }
                    else if (column.IsForeignKey)
                    {
                        colIcon = "LinkVariant";
                        badgeColor = UiColorConstants.C_F87171; // Red
                    }
                    else if (column.IsIndexed)
                    {
                        colIcon = "DatabaseSearch";
                        badgeColor = UiColorConstants.C_60A5FA; // Blue
                    }
                    else
                    {
                        colIcon = "CircleOutline";
                        badgeColor = UiColorConstants.C_9CA3AF; // Gray
                    }

                    var colItem = new SchemaObjectViewModel(
                        column.Name,
                        colIcon,
                        column.DataType ?? "unknown",
                        column.DataType ?? "unknown",
                        badgeColor,
                        column);

                    item.Children.Add(colItem);
                }

                string searchText = string.Join(' ',
                    table.Name,
                    table.Schema ?? string.Empty,
                    string.Join(' ', table.Columns.Select(c => c.Name)));

                catalog.Add(new SchemaCatalogEntry(
                    item,
                    table.Kind != TableKind.Table,
                    searchText));
            }
        }

        _catalogEntries = catalog;
    }

    private void ApplyFilterToCategories()
    {
        Categories.Clear();
        _visibleObjectCount = 0;

        if (_catalogEntries.Count == 0)
        {
            RaisePropertyChanged(nameof(ShowNoConnectionState));
            RaisePropertyChanged(nameof(ShowLoadingState));
            RaisePropertyChanged(nameof(ShowFilterEmptyState));
            RaisePropertyChanged(nameof(ShowNoTablesState));
            return;
        }

        var tablesCategory = new SchemaCategoryViewModel("Tables", "Table", UiColorConstants.C_60A5FA);
        var viewsCategory = new SchemaCategoryViewModel("Views", "Eye", UiColorConstants.C_34D399);

        IEnumerable<SchemaCatalogEntry> filteredEntries = _catalogEntries;
        if (!string.IsNullOrWhiteSpace(FilterQuery))
        {
            filteredEntries = filteredEntries.Where(entry =>
                TextSearch.MatchesContainsAllTokens(FilterQuery, entry.SearchText));
        }

        foreach (SchemaCatalogEntry entry in filteredEntries)
        {
            if (entry.IsView)
                viewsCategory.Items.Add(entry.Item);
            else
                tablesCategory.Items.Add(entry.Item);
        }

        _visibleObjectCount = tablesCategory.Items.Count + viewsCategory.Items.Count;

        if (tablesCategory.Items.Count > 0)
            Categories.Add(tablesCategory);

        if (viewsCategory.Items.Count > 0)
            Categories.Add(viewsCategory);

        RaisePropertyChanged(nameof(ShowNoConnectionState));
        RaisePropertyChanged(nameof(ShowLoadingState));
        RaisePropertyChanged(nameof(ShowFilterEmptyState));
        RaisePropertyChanged(nameof(ShowNoTablesState));
    }

    public static PinDataType MapSqlTypeToPinDataType(string? rawType)
    {
        string normalized = (rawType ?? string.Empty).Trim().ToLowerInvariant();

        return normalized switch
        {
            "int" or "integer" or "bigint" or "smallint" or "tinyint" => PinDataType.Integer,
            "decimal" or "numeric" or "float" or "double" or "real" or "money" => PinDataType.Decimal,
            "varchar" or "nvarchar" or "text" or "char" or "nchar" or "string" => PinDataType.Text,
            "bool" or "boolean" or "bit" => PinDataType.Boolean,
            "datetime" or "timestamp" or "date" or "time" => PinDataType.DateTime,
            "json" or "jsonb" => PinDataType.Json,
            _ => PinDataType.Expression,
        };
    }
}


