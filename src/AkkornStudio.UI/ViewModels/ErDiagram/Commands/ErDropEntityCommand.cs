using AkkornStudio.Ddl;
using AkkornStudio.UI.ViewModels.UndoRedo;

namespace AkkornStudio.UI.ViewModels.ErDiagram.Commands;

/// <summary>
/// Drops an ER entity and all attached edges, and exposes equivalent DROP TABLE DDL.
/// </summary>
public sealed class ErDropEntityCommand(
    ErCanvasViewModel erCanvas,
    ErEntityNodeViewModel entity) : ICanvasCommand
{
    private readonly ErCanvasViewModel _erCanvas = erCanvas ?? throw new ArgumentNullException(nameof(erCanvas));
    private readonly ErEntityNodeViewModel _entity = entity ?? throw new ArgumentNullException(nameof(entity));
    private IReadOnlyList<ErRelationEdgeViewModel> _removedEdges = [];

    public string Description => "ER: drop entity";

    public void Execute(CanvasViewModel canvas)
    {
        _ = canvas;

        _removedEdges = _erCanvas.GetEdgesForEntity(_entity.Id);
        foreach (ErRelationEdgeViewModel edge in _removedEdges)
            _erCanvas.Edges.Remove(edge);

        _erCanvas.Entities.Remove(_entity);
        if (ReferenceEquals(_erCanvas.SelectedEntity, _entity))
            _erCanvas.ClearSelection();
    }

    public void Undo(CanvasViewModel canvas)
    {
        _ = canvas;

        if (!_erCanvas.Entities.Contains(_entity))
            _erCanvas.Entities.Add(_entity);

        foreach (ErRelationEdgeViewModel edge in _removedEdges)
        {
            if (!_erCanvas.Edges.Contains(edge))
                _erCanvas.Edges.Add(edge);
        }
    }

    public IDdlExpression ToDdlExpression()
    {
        (string schema, string table) = SplitEntityId(_entity.Id);
        return new DropTableExpr(schema, table, ifExists: false);
    }

    private static (string Schema, string Table) SplitEntityId(string entityId)
    {
        int separator = entityId.IndexOf('.', StringComparison.Ordinal);
        if (separator <= 0)
            return (string.Empty, entityId.Trim());

        return (entityId[..separator].Trim(), entityId[(separator + 1)..].Trim());
    }
}
