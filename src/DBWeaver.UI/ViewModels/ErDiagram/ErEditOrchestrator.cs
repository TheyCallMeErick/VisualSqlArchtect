using DBWeaver.Core;
using DBWeaver.UI.Services.SqlEditor;
using DBWeaver.UI.ViewModels.ErDiagram.Commands;
using DBWeaver.UI.ViewModels.UndoRedo;

namespace DBWeaver.UI.ViewModels.ErDiagram;

/// <summary>
/// Coordinates ER edit command execution, DDL generation, mutation guard and undo/redo integration.
/// </summary>
public sealed class ErEditOrchestrator
{
    private readonly ErCanvasViewModel _erCanvas;
    private readonly MutationGuardService _mutationGuardService;
    private readonly ErDdlEmitter _ddlEmitter;

    public ErEditOrchestrator(
        ErCanvasViewModel erCanvas,
        DatabaseProvider provider,
        MutationGuardService mutationGuardService)
    {
        _erCanvas = erCanvas ?? throw new ArgumentNullException(nameof(erCanvas));
        _mutationGuardService = mutationGuardService ?? throw new ArgumentNullException(nameof(mutationGuardService));
        _ddlEmitter = new ErDdlEmitter(provider);
    }

    public string LastGeneratedDdl { get; private set; } = string.Empty;

    public bool ApplyCommand(
        ICanvasCommand command,
        CanvasViewModel canvas,
        UndoRedoStack? undoRedo = null,
        bool force = false)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(canvas);

        string ddl = _ddlEmitter.Emit([command]);
        LastGeneratedDdl = ddl;

        if (!force && IsDestructive(command))
        {
            MutationGuardResult guard = _mutationGuardService.Analyze(ddl);
            if (guard.RequiresConfirmation)
                return false;
        }

        if (undoRedo is not null)
            undoRedo.Execute(command);
        else
            command.Execute(canvas);

        return true;
    }

    private static bool IsDestructive(ICanvasCommand command) =>
        command is ErDropEntityCommand
            or ErRemoveColumnCommand
            or ErRemoveForeignKeyCommand;
}
