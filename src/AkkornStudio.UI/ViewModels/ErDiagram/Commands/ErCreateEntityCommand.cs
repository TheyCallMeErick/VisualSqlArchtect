using AkkornStudio.Ddl;
using AkkornStudio.UI.ViewModels.UndoRedo;

namespace AkkornStudio.UI.ViewModels.ErDiagram.Commands;

/// <summary>
/// Creates a new ER entity and exposes equivalent CREATE TABLE DDL.
/// </summary>
public sealed class ErCreateEntityCommand(
    ErCanvasViewModel erCanvas,
    ErEntityNodeViewModel entity) : ICanvasCommand
{
    private readonly ErCanvasViewModel _erCanvas = erCanvas ?? throw new ArgumentNullException(nameof(erCanvas));
    private readonly ErEntityNodeViewModel _entity = entity ?? throw new ArgumentNullException(nameof(entity));

    public string Description => "ER: create entity";

    public void Execute(CanvasViewModel canvas)
    {
        _ = canvas;
        if (!_erCanvas.Entities.Contains(_entity))
            _erCanvas.Entities.Add(_entity);
    }

    public void Undo(CanvasViewModel canvas)
    {
        _ = canvas;
        _erCanvas.Entities.Remove(_entity);
    }

    public IDdlExpression ToDdlExpression()
    {
        (string schema, string table) = SplitEntityId(_entity.Id);
        IReadOnlyList<DdlColumnExpr> columns =
        [
            .. _entity.Columns.Select(col => new DdlColumnExpr(
                ColumnName: col.ColumnName,
                DataType: col.DataType,
                IsNullable: col.IsNullable,
                DefaultExpression: null,
                Comment: col.Comment)),
        ];

        return new CreateTableExpr(
            schemaName: schema,
            tableName: table,
            ifNotExists: false,
            columns: columns,
            primaryKeys: [],
            uniques: [],
            checks: [],
            tableComment: null,
            mode: DdlIdempotentMode.None,
            foreignKeys: []);
    }

    private static (string Schema, string Table) SplitEntityId(string entityId)
    {
        int separator = entityId.IndexOf('.', StringComparison.Ordinal);
        if (separator <= 0)
            return (string.Empty, entityId.Trim());

        return (entityId[..separator].Trim(), entityId[(separator + 1)..].Trim());
    }
}
