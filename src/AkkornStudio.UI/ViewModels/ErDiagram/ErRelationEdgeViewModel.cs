using Avalonia;
using AkkornStudio.Metadata;
using AkkornStudio.UI.ViewModels;

namespace AkkornStudio.UI.ViewModels.ErDiagram;

/// <summary>
/// Represents a child-to-parent FK relationship edge in the ER canvas.
/// Supports both single-column and composite relationships.
/// </summary>
public sealed class ErRelationEdgeViewModel : ViewModelBase
{
    private string _childEntityId;
    private string _parentEntityId;
    private bool _isSelected;
    private double _startX;
    private double _startY;
    private double _endX;
    private double _endY;
    private IReadOnlyList<Point> _routePoints = [];

    public ErRelationEdgeViewModel(
        string? constraintName,
        string childEntityId,
        string parentEntityId,
        string childColumn,
        string parentColumn,
        ReferentialAction onDelete,
        ReferentialAction onUpdate)
        : this(
            constraintName,
            childEntityId,
            parentEntityId,
            [childColumn],
            [parentColumn],
            onDelete,
            onUpdate)
    {
    }

    public ErRelationEdgeViewModel(
        string? constraintName,
        string childEntityId,
        string parentEntityId,
        IReadOnlyList<string> childColumns,
        IReadOnlyList<string> parentColumns,
        ReferentialAction onDelete,
        ReferentialAction onUpdate)
    {
        ConstraintName = constraintName;
        _childEntityId = childEntityId;
        _parentEntityId = parentEntityId;
        ChildColumns = childColumns.Count == 0 ? [] : [.. childColumns];
        ParentColumns = parentColumns.Count == 0 ? [] : [.. parentColumns];
        OnDelete = onDelete;
        OnUpdate = onUpdate;
    }

    public string? ConstraintName { get; }

    public string ChildEntityId
    {
        get => _childEntityId;
        set
        {
            if (!Set(ref _childEntityId, value))
                return;

            RaisePropertyChanged(nameof(TooltipText));
        }
    }

    public string ParentEntityId
    {
        get => _parentEntityId;
        set
        {
            if (!Set(ref _parentEntityId, value))
                return;

            RaisePropertyChanged(nameof(TooltipText));
        }
    }

    public IReadOnlyList<string> ChildColumns { get; }

    public IReadOnlyList<string> ParentColumns { get; }

    public string ChildColumn => ChildColumns.FirstOrDefault() ?? string.Empty;

    public string ParentColumn => ParentColumns.FirstOrDefault() ?? string.Empty;

    public ReferentialAction OnDelete { get; }

    public ReferentialAction OnUpdate { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set => Set(ref _isSelected, value);
    }

    public double StartX
    {
        get => _startX;
        set
        {
            if (!Set(ref _startX, value))
                return;

            RaisePropertyChanged(nameof(StartPoint));
            RaisePropertyChanged(nameof(MidpointX));
        }
    }

    public double StartY
    {
        get => _startY;
        set
        {
            if (!Set(ref _startY, value))
                return;

            RaisePropertyChanged(nameof(StartPoint));
            RaisePropertyChanged(nameof(MidpointY));
        }
    }

    public double EndX
    {
        get => _endX;
        set
        {
            if (!Set(ref _endX, value))
                return;

            RaisePropertyChanged(nameof(EndPoint));
            RaisePropertyChanged(nameof(MidpointX));
        }
    }

    public double EndY
    {
        get => _endY;
        set
        {
            if (!Set(ref _endY, value))
                return;

            RaisePropertyChanged(nameof(EndPoint));
            RaisePropertyChanged(nameof(MidpointY));
        }
    }

    public double MidpointX => (StartX + EndX) / 2d;

    public double MidpointY => (StartY + EndY) / 2d;

    public IReadOnlyList<Point> RoutePoints
    {
        get => _routePoints;
        private set => Set(ref _routePoints, value);
    }

    public Point StartPoint => new(StartX, StartY);

    public Point EndPoint => new(EndX, EndY);

    public double LabelX => RoutePoints.Count >= 3 ? RoutePoints[1].X : MidpointX;

    public double LabelY => RoutePoints.Count >= 3 ? (RoutePoints[1].Y + RoutePoints[2].Y) / 2d : MidpointY;

    public string Cardinality => ChildColumns.Count > 1 ? $"N:1 ({ChildColumns.Count})" : "N:1";

    public int ColumnPairCount => Math.Min(ChildColumns.Count, ParentColumns.Count);

    public string ConstraintLabel => string.IsNullOrWhiteSpace(ConstraintName) ? "Relacionamento sem nome" : ConstraintName;

    public string MappingSummary =>
        string.Join(" | ", ChildColumns.Zip(ParentColumns, static (child, parent) => $"{child} -> {parent}"));

    public string JoinPredicateSql =>
        string.Join(
            " AND ",
            ChildColumns.Zip(
                ParentColumns,
                (child, parent) => $"{ChildEntityId}.{child} = {ParentEntityId}.{parent}"));

    public string TooltipText =>
        $"{ChildEntityId}.{FormatColumnList(ChildColumns)} -> {ParentEntityId}.{FormatColumnList(ParentColumns)}";

    public void SetRoute(IEnumerable<Point> routePoints)
    {
        RoutePoints = routePoints.ToArray();
        RaisePropertyChanged(nameof(LabelX));
        RaisePropertyChanged(nameof(LabelY));
    }

    private static string FormatColumnList(IReadOnlyList<string> columns) =>
        columns.Count <= 1 ? (columns.FirstOrDefault() ?? string.Empty) : $"({string.Join(", ", columns)})";
}
