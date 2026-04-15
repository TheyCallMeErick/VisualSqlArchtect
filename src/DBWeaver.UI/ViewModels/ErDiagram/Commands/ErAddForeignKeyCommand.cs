using DBWeaver.Ddl;
using DBWeaver.Metadata;
using DBWeaver.UI.ViewModels.UndoRedo;

namespace DBWeaver.UI.ViewModels.ErDiagram.Commands;

/// <summary>
/// Adds a simple foreign key edge to the ER canvas and exposes equivalent DDL.
/// </summary>
public sealed class ErAddForeignKeyCommand(
    ErCanvasViewModel erCanvas,
    string? constraintName,
    string childEntityId,
    string parentEntityId,
    string childColumn,
    string parentColumn,
    ReferentialAction onDelete,
    ReferentialAction onUpdate) : ICanvasCommand
{
    private readonly ErCanvasViewModel _erCanvas = erCanvas ?? throw new ArgumentNullException(nameof(erCanvas));
    private readonly ErRelationEdgeViewModel _edge = new(
        constraintName,
        childEntityId,
        parentEntityId,
        childColumn,
        parentColumn,
        onDelete,
        onUpdate);

    public string Description => "ER: add foreign key";

    public void Execute(CanvasViewModel canvas)
    {
        _ = canvas;
        if (!_erCanvas.Edges.Contains(_edge))
            _erCanvas.Edges.Add(_edge);
    }

    public void Undo(CanvasViewModel canvas)
    {
        _ = canvas;
        _erCanvas.Edges.Remove(_edge);
    }

    public IDdlExpression ToDdlExpression()
    {
        (string childSchema, string childTable) = SplitEntityId(_edge.ChildEntityId);
        (string parentSchema, string parentTable) = SplitEntityId(_edge.ParentEntityId);

        return new AlterTableExpr(
            schemaName: childSchema,
            tableName: childTable,
            operations:
            [
                new AddForeignKeyOpExpr(
                    constraintName: _edge.ConstraintName,
                    childColumn: _edge.ChildColumn,
                    parentSchema: parentSchema,
                    parentTable: parentTable,
                    parentColumn: _edge.ParentColumn,
                    onDelete: _edge.OnDelete,
                    onUpdate: _edge.OnUpdate),
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
