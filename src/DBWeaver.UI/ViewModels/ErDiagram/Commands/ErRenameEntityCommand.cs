using DBWeaver.Ddl;
using DBWeaver.UI.ViewModels.UndoRedo;

namespace DBWeaver.UI.ViewModels.ErDiagram.Commands;

/// <summary>
/// Renames an ER entity and updates all connected edge endpoint ids.
/// </summary>
public sealed class ErRenameEntityCommand(
    ErCanvasViewModel erCanvas,
    ErEntityNodeViewModel entity,
    string newSchema,
    string newName) : ICanvasCommand
{
    private readonly ErCanvasViewModel _erCanvas = erCanvas ?? throw new ArgumentNullException(nameof(erCanvas));
    private readonly ErEntityNodeViewModel _entity = entity ?? throw new ArgumentNullException(nameof(entity));
    private readonly string _newSchema = newSchema ?? string.Empty;
    private readonly string _newName = newName ?? string.Empty;

    private string? _oldSchema;
    private string? _oldName;
    private string? _oldId;
    private bool _wasSelected;
    private bool _captured;

    public string Description => "ER: rename entity";

    public void Execute(CanvasViewModel canvas)
    {
        _ = canvas;

        if (!_captured)
        {
            _oldSchema = _entity.Schema;
            _oldName = _entity.Name;
            _oldId = _entity.Id;
            _wasSelected = ReferenceEquals(_erCanvas.SelectedEntity, _entity);
            _captured = true;
        }

        string previousId = _entity.Id;
        _entity.Rename(_newSchema, _newName);
        string currentId = _entity.Id;

        foreach (ErRelationEdgeViewModel edge in _erCanvas.Edges)
        {
            if (string.Equals(edge.ChildEntityId, previousId, StringComparison.OrdinalIgnoreCase))
                edge.ChildEntityId = currentId;

            if (string.Equals(edge.ParentEntityId, previousId, StringComparison.OrdinalIgnoreCase))
                edge.ParentEntityId = currentId;
        }

        if (_wasSelected)
            _erCanvas.SelectedEntity = _entity;
    }

    public void Undo(CanvasViewModel canvas)
    {
        _ = canvas;
        if (!_captured || _oldSchema is null || _oldName is null || _oldId is null)
            return;

        string currentId = _entity.Id;
        _entity.Rename(_oldSchema, _oldName);

        foreach (ErRelationEdgeViewModel edge in _erCanvas.Edges)
        {
            if (string.Equals(edge.ChildEntityId, currentId, StringComparison.OrdinalIgnoreCase))
                edge.ChildEntityId = _oldId;

            if (string.Equals(edge.ParentEntityId, currentId, StringComparison.OrdinalIgnoreCase))
                edge.ParentEntityId = _oldId;
        }

        if (_wasSelected)
            _erCanvas.SelectedEntity = _entity;
    }

    public IDdlExpression ToDdlExpression()
    {
        if (!_captured || _oldSchema is null || _oldName is null)
            throw new InvalidOperationException("Execute command before generating rename DDL.");

        return new AlterTableExpr(
            schemaName: _oldSchema,
            tableName: _oldName,
            operations:
            [
                new RenameTableOpExpr(_newName, _newSchema),
            ],
            emitSeparateStatements: true);
    }
}
