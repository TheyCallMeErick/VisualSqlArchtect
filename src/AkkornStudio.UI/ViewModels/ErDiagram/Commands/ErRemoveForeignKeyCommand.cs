using AkkornStudio.Ddl;
using AkkornStudio.UI.ViewModels.UndoRedo;

namespace AkkornStudio.UI.ViewModels.ErDiagram.Commands;

/// <summary>
/// Removes a simple foreign key edge from the ER canvas and exposes equivalent DDL.
/// </summary>
public sealed class ErRemoveForeignKeyCommand(
    ErCanvasViewModel erCanvas,
    ErRelationEdgeViewModel edge) : ICanvasCommand
{
    private readonly ErCanvasViewModel _erCanvas = erCanvas ?? throw new ArgumentNullException(nameof(erCanvas));
    private readonly ErRelationEdgeViewModel _edge = edge ?? throw new ArgumentNullException(nameof(edge));

    public string Description => "ER: remove foreign key";

    public void Execute(CanvasViewModel canvas)
    {
        _ = canvas;
        _erCanvas.Edges.Remove(_edge);
    }

    public void Undo(CanvasViewModel canvas)
    {
        _ = canvas;
        if (!_erCanvas.Edges.Contains(_edge))
            _erCanvas.Edges.Add(_edge);
    }

    public IDdlExpression ToDdlExpression()
    {
        if (string.IsNullOrWhiteSpace(_edge.ConstraintName))
            throw new InvalidOperationException("ErRemoveForeignKeyCommand requires non-empty constraint name.");

        (string childSchema, string childTable) = SplitEntityId(_edge.ChildEntityId);
        return new AlterTableExpr(
            schemaName: childSchema,
            tableName: childTable,
            operations:
            [
                new DropConstraintOpExpr(_edge.ConstraintName),
            ],
            emitSeparateStatements: true);
    }

    private static (string Schema, string Table) SplitEntityId(string entityId)
    {
        int separator = entityId.IndexOf('.', StringComparison.Ordinal);
        if (separator <= 0)
            return (string.Empty, entityId.Trim());

        return (entityId[..separator].Trim(), entityId[(separator + 1)..].Trim());
    }
}
