using System.Collections.ObjectModel;
using System.Collections.Specialized;
using AkkornStudio.UI.ViewModels;

namespace AkkornStudio.UI.ViewModels.ErDiagram;

/// <summary>
/// Represents the full ER canvas state for read-only schema visualization.
/// </summary>
public sealed class ErCanvasViewModel : ViewModelBase
{
    private ErEntityNodeViewModel? _selectedEntity;
    private ErRelationEdgeViewModel? _selectedEdge;
    private double _viewportX;
    private double _viewportY;
    private double _zoom = 1.0;
    private bool _includeViews;

    public ErCanvasViewModel()
    {
        Entities = [];
        Edges = [];
        TechnicalWarnings = [];

        Entities.CollectionChanged += OnEntitiesChanged;
        Edges.CollectionChanged += OnEdgesChanged;
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
                SelectedEdge = null;
        }
    }

    public ErRelationEdgeViewModel? SelectedEdge
    {
        get => _selectedEdge;
        set
        {
            if (!Set(ref _selectedEdge, value))
                return;

            if (value is not null)
                SelectedEntity = null;
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
        set => Set(ref _includeViews, value);
    }

    public int EntityCount => Entities.Count;

    public int EdgeCount => Edges.Count;

    public void ClearSelection()
    {
        if (SelectedEntity is not null)
            SelectedEntity.IsSelected = false;

        SelectedEntity = null;
        SelectedEdge = null;
    }

    public void ReplaceContents(ErCanvasViewModel source)
    {
        ArgumentNullException.ThrowIfNull(source);

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

    private void OnEntitiesChanged(object? sender, NotifyCollectionChangedEventArgs e)
        => RaisePropertyChanged(nameof(EntityCount));

    private void OnEdgesChanged(object? sender, NotifyCollectionChangedEventArgs e)
        => RaisePropertyChanged(nameof(EdgeCount));
}
