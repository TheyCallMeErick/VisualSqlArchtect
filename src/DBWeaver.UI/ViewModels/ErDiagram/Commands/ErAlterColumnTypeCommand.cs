using DBWeaver.Ddl;
using DBWeaver.UI.ViewModels.UndoRedo;

namespace DBWeaver.UI.ViewModels.ErDiagram.Commands;

/// <summary>
/// Changes only the data type of an existing ER column while preserving all other flags.
/// </summary>
public sealed class ErAlterColumnTypeCommand(
    ErEntityNodeViewModel entity,
    string columnName,
    string newDataType) : ICanvasCommand
{
    private readonly ErEntityNodeViewModel _entity = entity ?? throw new ArgumentNullException(nameof(entity));
    private readonly string _columnName = columnName ?? throw new ArgumentNullException(nameof(columnName));
    private readonly string _newDataType = newDataType ?? throw new ArgumentNullException(nameof(newDataType));

    private int _index = -1;
    private ErColumnRowViewModel? _oldColumn;
    private ErColumnRowViewModel? _newColumn;

    public string Description => "ER: alter column type";

    public void Execute(CanvasViewModel canvas)
    {
        _ = canvas;

        _index = _entity.Columns
            .Select((col, idx) => new { col, idx })
            .FirstOrDefault(x => string.Equals(x.col.ColumnName, _columnName, StringComparison.OrdinalIgnoreCase))
            ?.idx ?? -1;

        if (_index < 0)
            return;

        _oldColumn = _entity.Columns[_index];
        _newColumn = new ErColumnRowViewModel(
            columnName: _oldColumn.ColumnName,
            dataType: _newDataType,
            isNullable: _oldColumn.IsNullable,
            isPrimaryKey: _oldColumn.IsPrimaryKey,
            isForeignKey: _oldColumn.IsForeignKey,
            isUnique: _oldColumn.IsUnique,
            comment: _oldColumn.Comment);

        _entity.Columns[_index] = _newColumn;
    }

    public void Undo(CanvasViewModel canvas)
    {
        _ = canvas;
        if (_index < 0 || _oldColumn is null)
            return;

        _entity.Columns[_index] = _oldColumn;
    }

    public IDdlExpression ToDdlExpression()
    {
        (string schema, string table) = SplitEntityId(_entity.Id);

        return new AlterTableExpr(
            schemaName: schema,
            tableName: table,
            operations:
            [
                new AlterColumnTypeOpExpr(_columnName, _newDataType, _oldColumn?.IsNullable ?? true),
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
