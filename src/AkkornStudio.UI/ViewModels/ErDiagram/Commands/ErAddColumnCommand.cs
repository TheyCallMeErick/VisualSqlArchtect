using AkkornStudio.Ddl;
using AkkornStudio.UI.ViewModels.UndoRedo;

namespace AkkornStudio.UI.ViewModels.ErDiagram.Commands;

/// <summary>
/// Adds a new column at the end of an ER entity and exposes equivalent DDL.
/// </summary>
public sealed class ErAddColumnCommand(
    ErEntityNodeViewModel entity,
    ErColumnRowViewModel column) : ICanvasCommand
{
    private readonly ErEntityNodeViewModel _entity = entity ?? throw new ArgumentNullException(nameof(entity));
    private readonly ErColumnRowViewModel _column = column ?? throw new ArgumentNullException(nameof(column));

    public string Description => "ER: add column";

    public void Execute(CanvasViewModel canvas)
    {
        _ = canvas;
        if (!_entity.Columns.Contains(_column))
            _entity.Columns.Add(_column);
    }

    public void Undo(CanvasViewModel canvas)
    {
        _ = canvas;
        _entity.Columns.Remove(_column);
    }

    public IDdlExpression ToDdlExpression()
    {
        (string schema, string table) = SplitEntityId(_entity.Id);

        return new AlterTableExpr(
            schemaName: schema,
            tableName: table,
            operations:
            [
                new AddColumnOpExpr(new DdlColumnExpr(
                    ColumnName: _column.ColumnName,
                    DataType: _column.DataType,
                    IsNullable: _column.IsNullable,
                    DefaultExpression: null,
                    Comment: _column.Comment)),
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
