using DBWeaver.Metadata;
using DBWeaver.UI.ViewModels;

namespace DBWeaver.UI.ViewModels.ErDiagram;

/// <summary>
/// Represents a simple child-to-parent FK relationship edge in the ER canvas.
/// </summary>
public sealed class ErRelationEdgeViewModel : ViewModelBase
{
    private string _childEntityId;
    private string _parentEntityId;

    public ErRelationEdgeViewModel(
        string? constraintName,
        string childEntityId,
        string parentEntityId,
        string childColumn,
        string parentColumn,
        ReferentialAction onDelete,
        ReferentialAction onUpdate)
    {
        ConstraintName = constraintName;
        _childEntityId = childEntityId;
        _parentEntityId = parentEntityId;
        ChildColumn = childColumn;
        ParentColumn = parentColumn;
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

    public string ChildColumn { get; }

    public string ParentColumn { get; }

    public ReferentialAction OnDelete { get; }

    public ReferentialAction OnUpdate { get; }

    public string Cardinality => "N:1";

    public string TooltipText => $"{ChildEntityId}.{ChildColumn} → {ParentEntityId}.{ParentColumn}";
}
