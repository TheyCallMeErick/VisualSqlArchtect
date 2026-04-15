using AkkornStudio.Ddl;
using AkkornStudio.UI.ViewModels.UndoRedo;

namespace AkkornStudio.UI.ViewModels.ErDiagram.Commands;

/// <summary>
/// Removes an entity column while preserving the original index for undo.
/// </summary>
public sealed class ErRemoveColumnCommand(
    ErEntityNodeViewModel entity,
    string columnName) : ICanvasCommand
{
    private readonly ErEntityNodeViewModel _entity = entity ?? throw new ArgumentNullException(nameof(entity));
    private readonly string _columnName = columnName ?? throw new ArgumentNullException(nameof(columnName));

    private int _removedIndex = -1;
    private ErColumnRowViewModel? _removedColumn;

    public string Description => "ER: remove column";

    public void Execute(CanvasViewModel canvas)
    {
        _ = canvas;

        int index = _entity.Columns
            .Select((col, idx) => new { col, idx })
            .FirstOrDefault(x => string.Equals(x.col.ColumnName, _columnName, StringComparison.OrdinalIgnoreCase))
            ?.idx ?? -1;

        if (index < 0)
            return;

        _removedIndex = index;
        _removedColumn = _entity.Columns[index];
        _entity.Columns.RemoveAt(index);
    }

    public void Undo(CanvasViewModel canvas)
    {
        _ = canvas;
        if (_removedColumn is null || _removedIndex < 0)
            return;

        int restoreIndex = Math.Min(_removedIndex, _entity.Columns.Count);
        _entity.Columns.Insert(restoreIndex, _removedColumn);
    }

    public IDdlExpression ToDdlExpression()
    {
        (string schema, string table) = SplitEntityId(_entity.Id);

        return new AlterTableExpr(
            schemaName: schema,
            tableName: table,
            operations:
            [
                new DropColumnOpExpr(_columnName, ifExists: false),
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
