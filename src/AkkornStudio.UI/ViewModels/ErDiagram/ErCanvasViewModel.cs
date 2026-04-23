using System.Collections.ObjectModel;
using System.Collections.Specialized;
using AkkornStudio.Metadata;
using AkkornStudio.UI.ViewModels;

namespace AkkornStudio.UI.ViewModels.ErDiagram;

/// <summary>
/// Represents the full ER canvas state for read-only schema visualization.
/// </summary>
public sealed class ErCanvasViewModel : ViewModelBase
{
    private const double EntityWidth = 220d;
    private const double HeaderHeight = 36d;
    private const double ColumnRowHeight = 22d;

    private Action<ErRelationEdgeViewModel>? _openSelectionInQuery;
    private ErEntityNodeViewModel? _selectedEntity;
    private ErRelationEdgeViewModel? _selectedEdge;
    private double _viewportX;
    private double _viewportY;
    private double _zoom = 1.0;
    private double _focusTargetX;
    private double _focusTargetY;
    private int _focusRequestVersion;
    private bool _includeViews;
    private bool _isRebuilding;
    private DbMetadata? _sourceMetadata;

    public ErCanvasViewModel()
    {
        Entities = [];
        Edges = [];
        TechnicalWarnings = [];
        RefreshCommand = new RelayCommand(RefreshFromSourceMetadata);
        OpenSelectionInQueryCommand = new RelayCommand(OpenSelectionInQuery, CanOpenSelectionInQuery);

        Entities.CollectionChanged += OnEntitiesChanged;
        Edges.CollectionChanged += OnEdgesChanged;
        TechnicalWarnings.CollectionChanged += OnTechnicalWarningsChanged;
    }

    public ObservableCollection<ErEntityNodeViewModel> Entities { get; }

    public ObservableCollection<ErRelationEdgeViewModel> Edges { get; }

    public ObservableCollection<string> TechnicalWarnings { get; }

    public ErEntityNodeViewModel? SelectedEntity
    {
        get => _selectedEntity;
        set
        {
            if (ReferenceEquals(_selectedEntity, value))
                return;

            if (_selectedEntity is not null)
                _selectedEntity.IsSelected = false;

            if (!Set(ref _selectedEntity, value))
                return;

            if (_selectedEntity is not null)
                _selectedEntity.IsSelected = true;

            if (value is not null)
            {
                if (_selectedEdge is not null)
                    _selectedEdge.IsSelected = false;

                _selectedEdge = null;
                RaisePropertyChanged(nameof(SelectedEdge));
            }

            ApplySelectionHighlights();
            RaiseSelectionDetailPropertiesChanged();
        }
    }

    public ErRelationEdgeViewModel? SelectedEdge
    {
        get => _selectedEdge;
        set
        {
            if (ReferenceEquals(_selectedEdge, value))
                return;

            if (_selectedEdge is not null)
                _selectedEdge.IsSelected = false;

            if (!Set(ref _selectedEdge, value))
                return;

            if (_selectedEdge is not null)
                _selectedEdge.IsSelected = true;

            if (value is not null)
            {
                if (_selectedEntity is not null)
                    _selectedEntity.IsSelected = false;

                _selectedEntity = null;
                RaisePropertyChanged(nameof(SelectedEntity));
            }

            ApplySelectionHighlights();
            RaiseSelectionDetailPropertiesChanged();
        }
    }

    public double ViewportX
    {
        get => _viewportX;
        set => Set(ref _viewportX, value);
    }

    public double ViewportY
    {
        get => _viewportY;
        set => Set(ref _viewportY, value);
    }

    public double Zoom
    {
        get => _zoom;
        set => Set(ref _zoom, value);
    }

    public bool IncludeViews
    {
        get => _includeViews;
        set
        {
            if (!Set(ref _includeViews, value))
                return;

            if (!_isRebuilding)
                RefreshFromSourceMetadata();
        }
    }

    public int EntityCount => Entities.Count;

    public int EdgeCount => Edges.Count;

    public bool HasTechnicalWarnings => TechnicalWarnings.Count > 0;

    public bool HasSelectionDetails => SelectedEntity is not null || SelectedEdge is not null;

    public string StatusMessage =>
        _sourceMetadata is null
            ? "Conecte-se a um banco para gerar o diagrama ER."
            : $"Fonte sincronizada com {EntityCount} entidade(s) e {EdgeCount} relaçao(oes).";

    public string SelectionTitle =>
        SelectedEdge is not null
            ? SelectedEdge.ConstraintLabel
            : SelectedEntity is not null
                ? SelectedEntity.DisplayName
                : "Nada selecionado";

    public string SelectionSubtitle =>
        SelectedEdge is not null
            ? $"{SelectedEdge.ChildEntityId} -> {SelectedEdge.ParentEntityId}"
            : SelectedEntity is not null
                ? (SelectedEntity.IsView ? "View" : "Tabela")
                : "Selecione uma entidade ou relacionamento para ver os detalhes.";

    public string SelectionBody =>
        SelectedEdge is not null
            ? $"Mapeamento: {SelectedEdge.MappingSummary}"
            : SelectedEntity is not null
                ? SelectedEntity.SelectionSummary
                : string.Empty;

    public string SelectionJoinPredicate => SelectedEdge?.JoinPredicateSql ?? string.Empty;

    public bool HasSelectionJoinPredicate => SelectedEdge is not null;

    public double FocusTargetX
    {
        get => _focusTargetX;
        private set => Set(ref _focusTargetX, value);
    }

    public double FocusTargetY
    {
        get => _focusTargetY;
        private set => Set(ref _focusTargetY, value);
    }

    public int FocusRequestVersion
    {
        get => _focusRequestVersion;
        private set => Set(ref _focusRequestVersion, value);
    }

    public RelayCommand RefreshCommand { get; }

    public RelayCommand OpenSelectionInQueryCommand { get; }

    public void BindSourceMetadata(DbMetadata? metadata, bool rebuild = true)
    {
        _sourceMetadata = metadata;
        RaisePropertyChanged(nameof(StatusMessage));

        if (rebuild)
            RefreshFromSourceMetadata();
    }

    public void BindQueryNavigation(Action<ErRelationEdgeViewModel>? openSelectionInQuery)
    {
        _openSelectionInQuery = openSelectionInQuery;
        OpenSelectionInQueryCommand.NotifyCanExecuteChanged();
    }

    public void ClearSelection()
    {
        if (SelectedEntity is not null)
            SelectedEntity.IsSelected = false;

        SelectedEntity = null;
        SelectedEdge = null;
        ApplySelectionHighlights();
        RaiseSelectionDetailPropertiesChanged();
    }

    public void ReplaceContents(ErCanvasViewModel source)
    {
        ArgumentNullException.ThrowIfNull(source);
        _isRebuilding = true;
        IncludeViews = source.IncludeViews;
        _isRebuilding = false;

        Entities.Clear();
        Edges.Clear();
        TechnicalWarnings.Clear();

        foreach (ErEntityNodeViewModel entity in source.Entities)
            Entities.Add(entity);

        foreach (ErRelationEdgeViewModel edge in source.Edges)
            Edges.Add(edge);

        foreach (string warning in source.TechnicalWarnings)
            TechnicalWarnings.Add(warning);

        ClearSelection();
        RaisePropertyChanged(nameof(EntityCount));
        RaisePropertyChanged(nameof(EdgeCount));
        RaisePropertyChanged(nameof(HasTechnicalWarnings));
        RaisePropertyChanged(nameof(StatusMessage));
        RaiseSelectionDetailPropertiesChanged();
    }

    public void AddTechnicalWarning(string warningCode)
    {
        if (string.IsNullOrWhiteSpace(warningCode))
            return;

        TechnicalWarnings.Add(warningCode.Trim());
    }

    public ErEntityNodeViewModel? FindEntity(string entityId)
    {
        if (string.IsNullOrWhiteSpace(entityId))
            return null;

        return Entities.FirstOrDefault(entity =>
            string.Equals(entity.Id, entityId, StringComparison.OrdinalIgnoreCase));
    }

    public IReadOnlyList<ErRelationEdgeViewModel> GetEdgesForEntity(string entityId)
    {
        if (string.IsNullOrWhiteSpace(entityId))
            return [];

        return Edges.Where(edge =>
                string.Equals(edge.ChildEntityId, entityId, StringComparison.OrdinalIgnoreCase)
                || string.Equals(edge.ParentEntityId, entityId, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    public bool TryFocusRelation(
        string childEntityId,
        string parentEntityId,
        IReadOnlyList<string>? childColumns = null,
        IReadOnlyList<string>? parentColumns = null)
    {
        if (string.IsNullOrWhiteSpace(childEntityId) || string.IsNullOrWhiteSpace(parentEntityId))
            return false;

        ErRelationEdgeViewModel? edge = Edges.FirstOrDefault(candidate =>
            string.Equals(candidate.ChildEntityId, childEntityId, StringComparison.OrdinalIgnoreCase)
            && string.Equals(candidate.ParentEntityId, parentEntityId, StringComparison.OrdinalIgnoreCase)
            && MatchesColumns(candidate.ChildColumns, childColumns)
            && MatchesColumns(candidate.ParentColumns, parentColumns));

        if (edge is null)
            return false;

        SelectedEdge = edge;
        RequestViewportFocusForCurrentSelection();
        return true;
    }

    public void RequestViewportFocusForCurrentSelection()
    {
        if (SelectedEdge is not null)
        {
            FocusTargetX = (SelectedEdge.StartX + SelectedEdge.EndX) / 2d;
            FocusTargetY = (SelectedEdge.StartY + SelectedEdge.EndY) / 2d;
            FocusRequestVersion++;
            return;
        }

        if (SelectedEntity is not null)
        {
            FocusTargetX = SelectedEntity.X + (EntityWidth / 2d);
            FocusTargetY = SelectedEntity.Y + GetEntityHeight(SelectedEntity) / 2d;
            FocusRequestVersion++;
        }
    }

    private void OnEntitiesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RaisePropertyChanged(nameof(EntityCount));
        RaisePropertyChanged(nameof(StatusMessage));
    }

    private void OnEdgesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RaisePropertyChanged(nameof(EdgeCount));
        RaisePropertyChanged(nameof(StatusMessage));
    }

    private void OnTechnicalWarningsChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        RaisePropertyChanged(nameof(HasTechnicalWarnings));

    private void RefreshFromSourceMetadata()
    {
        if (_sourceMetadata is null)
        {
            Entities.Clear();
            Edges.Clear();
            TechnicalWarnings.Clear();
            AddTechnicalWarning("W-ER-NO-METADATA");
            RaisePropertyChanged(nameof(StatusMessage));
            return;
        }

        ErCanvasViewModel rebuilt = ErCanvasBuilder.Build(_sourceMetadata, IncludeViews);
        rebuilt.BindSourceMetadata(_sourceMetadata, rebuild: false);
        ReplaceContents(rebuilt);
    }

    private void ApplySelectionHighlights()
    {
        Dictionary<string, HashSet<string>> highlights = new(StringComparer.OrdinalIgnoreCase);

        if (SelectedEdge is not null)
        {
            foreach (ErRelationEdgeViewModel edge in Edges)
                edge.IsSelected = ReferenceEquals(edge, SelectedEdge);

            AddColumnHighlights(highlights, SelectedEdge.ChildEntityId, SelectedEdge.ChildColumns);
            AddColumnHighlights(highlights, SelectedEdge.ParentEntityId, SelectedEdge.ParentColumns);
        }
        else if (SelectedEntity is not null)
        {
            HashSet<ErRelationEdgeViewModel> selectedEdges = [.. GetEdgesForEntity(SelectedEntity.Id)];
            foreach (ErRelationEdgeViewModel edge in Edges)
            {
                bool isSelected = selectedEdges.Contains(edge);
                edge.IsSelected = isSelected;
                if (!isSelected)
                    continue;

                AddColumnHighlights(highlights, edge.ChildEntityId, edge.ChildColumns);
                AddColumnHighlights(highlights, edge.ParentEntityId, edge.ParentColumns);
            }
        }
        else
        {
            foreach (ErRelationEdgeViewModel edge in Edges)
                edge.IsSelected = false;
        }

        foreach (ErEntityNodeViewModel entity in Entities)
        {
            if (!highlights.TryGetValue(entity.Id, out HashSet<string>? columns))
                columns = [];

            entity.HighlightColumns(columns);
        }
    }

    private static void AddColumnHighlight(
        IDictionary<string, HashSet<string>> highlights,
        string entityId,
        string columnName)
    {
        if (!highlights.TryGetValue(entityId, out HashSet<string>? columns))
        {
            columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            highlights[entityId] = columns;
        }

        columns.Add(columnName);
    }

    private static void AddColumnHighlights(
        IDictionary<string, HashSet<string>> highlights,
        string entityId,
        IReadOnlyList<string> columnNames)
    {
        foreach (string columnName in columnNames)
            AddColumnHighlight(highlights, entityId, columnName);
    }

    private void RaiseSelectionDetailPropertiesChanged()
    {
        RaisePropertyChanged(nameof(HasSelectionDetails));
        RaisePropertyChanged(nameof(SelectionTitle));
        RaisePropertyChanged(nameof(SelectionSubtitle));
        RaisePropertyChanged(nameof(SelectionBody));
        RaisePropertyChanged(nameof(SelectionJoinPredicate));
        RaisePropertyChanged(nameof(HasSelectionJoinPredicate));
        OpenSelectionInQueryCommand.NotifyCanExecuteChanged();
    }

    private static bool MatchesColumns(IReadOnlyList<string> candidate, IReadOnlyList<string>? expected)
    {
        if (expected is null || expected.Count == 0)
            return true;

        if (candidate.Count != expected.Count)
            return false;

        for (int i = 0; i < candidate.Count; i++)
        {
            if (!string.Equals(candidate[i], expected[i], StringComparison.OrdinalIgnoreCase))
                return false;
        }

        return true;
    }

    private static double GetEntityHeight(ErEntityNodeViewModel entity) =>
        HeaderHeight + (entity.Columns.Count * ColumnRowHeight);

    private bool CanOpenSelectionInQuery() => SelectedEdge is not null && _openSelectionInQuery is not null;

    private void OpenSelectionInQuery()
    {
        if (SelectedEdge is null || _openSelectionInQuery is null)
            return;

        _openSelectionInQuery(SelectedEdge);
    }
}
