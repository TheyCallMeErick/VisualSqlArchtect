using DBWeaver.Core;
using DBWeaver.Ddl;
using DBWeaver.UI.ViewModels.ErDiagram.Commands;
using DBWeaver.UI.ViewModels.UndoRedo;

namespace DBWeaver.UI.ViewModels.ErDiagram;

/// <summary>
/// Emits SQL DDL text from ER edit commands.
/// </summary>
public sealed class ErDdlEmitter(DatabaseProvider provider)
{
    private readonly DdlGeneratorService _generator = new(provider);

    public string Emit(IReadOnlyList<ICanvasCommand> commands)
    {
        if (commands is null || commands.Count == 0)
            return string.Empty;

        var statements = new List<IDdlExpression>();
        foreach (ICanvasCommand command in commands)
        {
            IDdlExpression? statement = command switch
            {
                ErAddForeignKeyCommand c => c.ToDdlExpression(),
                ErRemoveForeignKeyCommand c => c.ToDdlExpression(),
                ErRenameEntityCommand c => c.ToDdlExpression(),
                ErAddColumnCommand c => c.ToDdlExpression(),
                ErRemoveColumnCommand c => c.ToDdlExpression(),
                ErAlterColumnTypeCommand c => c.ToDdlExpression(),
                ErCreateEntityCommand c => c.ToDdlExpression(),
                ErDropEntityCommand c => c.ToDdlExpression(),
                _ => null,
            };

            if (statement is not null)
                statements.Add(statement);
        }

        if (statements.Count == 0)
            return string.Empty;

        return _generator.Generate(statements);
    }
}
