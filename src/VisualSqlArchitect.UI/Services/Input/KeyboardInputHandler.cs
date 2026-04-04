using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using VisualSqlArchitect.UI.Controls;
using VisualSqlArchitect.UI.ViewModels;

namespace VisualSqlArchitect.UI.Services;

/// <summary>
/// Centralizes keyboard input handling.
/// Routes 15+ keyboard shortcuts to appropriate commands and overlays.
/// Resolves the target canvas via <see cref="IActiveCanvasProvider"/> so that
/// shortcuts work for both the Query and DDL canvases.
/// </summary>
public class KeyboardInputHandler
{
    private readonly Window? _window;
    private readonly IActiveCanvasProvider _canvasProvider;
    private readonly FileOperationsService? _fileOps;
    private readonly Action? _onCreateNewCanvas;
    private readonly Action? _showShortcutsAction;
    private readonly Action? _openSearchAction;
    private readonly CommandPaletteViewModel? _commandPalette;

    private CanvasViewModel Vm => _canvasProvider.GetActive();

    /// <summary>
    /// Primary constructor used by the application shell.
    /// Accepts an <see cref="IActiveCanvasProvider"/> so shortcuts always route
    /// to whichever canvas (Query or DDL) is active at the time of the key press.
    /// </summary>
    public KeyboardInputHandler(
        Window window,
        IActiveCanvasProvider canvasProvider,
        FileOperationsService fileOps,
        CommandPaletteViewModel? commandPalette = null,
        Action? onCreateNewCanvas = null
    )
    {
        _window = window;
        _canvasProvider = canvasProvider;
        _fileOps = fileOps;
        _commandPalette = commandPalette;
        _onCreateNewCanvas = onCreateNewCanvas;
        _showShortcutsAction = null;
        _openSearchAction = null;
    }

    /// <summary>
    /// Test-friendly constructor that pins the handler to a fixed canvas.
    /// Internally wraps the canvas in a trivial <see cref="ActiveCanvasProvider"/>.
    /// </summary>
    public KeyboardInputHandler(
        CanvasViewModel vm,
        CommandPaletteViewModel? commandPalette = null,
        Action? onCreateNewCanvas = null,
        Action? showShortcutsAction = null,
        Action? openSearchAction = null
    )
    {
        _canvasProvider = new ActiveCanvasProvider(() => vm);
        _commandPalette = commandPalette;
        _onCreateNewCanvas = onCreateNewCanvas;
        _showShortcutsAction = showShortcutsAction;
        _openSearchAction = openSearchAction;
        _window = null;
        _fileOps = null;
    }

    /// <summary>
    /// Constructor that accepts an <see cref="IActiveCanvasProvider"/> without a <c>Window</c>.
    /// Used in tests and in headless scenarios where no window reference is available.
    /// </summary>
    public KeyboardInputHandler(IActiveCanvasProvider canvasProvider)
    {
        _canvasProvider = canvasProvider;
        _window = null;
        _fileOps = null;
        _commandPalette = null;
        _onCreateNewCanvas = null;
        _showShortcutsAction = null;
        _openSearchAction = null;
    }

    public void Wire()
    {
        if (_window is not null)
            _window.KeyDown += OnKeyDown;
    }

    public void OnKeyDown(object? s, KeyEventArgs e)
    {
        if (e.Handled)
            return;

        if (HandleShortcut(e.Key, e.KeyModifiers))
            e.Handled = true;
    }

    public bool HandleShortcut(Key key, KeyModifiers modifiers)
    {
        // Handle overlay escape keys
        if (key == Key.Escape)
        {
            if (_commandPalette?.IsVisible == true)
            {
                _commandPalette.Close();
                return true;
            }
            if (Vm.SearchMenu.IsVisible)
            {
                Vm.SearchMenu.Close();
                return true;
            }
            if (Vm.AutoJoin.IsVisible)
            {
                Vm.AutoJoin.Dismiss();
                return true;
            }
            if (Vm.DataPreview.IsVisible)
            {
                Vm.DataPreview.IsVisible = false;
                return true;
            }
            if (Vm.ConnectionManager.IsVisible)
            {
                Vm.ConnectionManager.IsVisible = false;
                return true;
            }
            if (Vm.Benchmark.IsVisible)
            {
                Vm.Benchmark.IsVisible = false;
                return true;
            }
            if (Vm.ExplainPlan.IsVisible)
            {
                Vm.ExplainPlan.Close();
                return true;
            }
            if (Vm.SqlImporter.IsVisible)
            {
                Vm.SqlImporter.Close();
                return true;
            }
            if (Vm.FlowVersions.IsVisible)
            {
                Vm.FlowVersions.Close();
                return true;
            }
            if (Vm.FileHistory.IsVisible)
            {
                Vm.FileHistory.Close();
                return true;
            }
            if (Vm.IsInCteEditor)
            {
                Vm.ExitCteEditorCommand.Execute(null);
                return true;
            }
        }

        if (key == Key.Enter && modifiers.HasFlag(KeyModifiers.Control) && modifiers.HasFlag(KeyModifiers.Alt))
        {
            if (Vm.IsInCteEditor)
                Vm.ExitCteEditorCommand.Execute(null);
            else
                Vm.EnterCteEditorCommand.Execute(null);

            return true;
        }

        // Shortcuts requiring overlay checks
        if (
            key == Key.A
            && modifiers.HasFlag(KeyModifiers.Shift)
            && !Vm.SearchMenu.IsVisible
        )
        {
            OpenSearch();
            return true;
        }
        if (
            key == Key.F
            && modifiers.HasFlag(KeyModifiers.Control)
            && !Vm.SearchMenu.IsVisible
        )
        {
            OpenSearch();
            return true;
        }

        // File operations
        if (key == Key.S && modifiers.HasFlag(KeyModifiers.Control) && _fileOps is not null)
        {
            _ = _fileOps.SaveAsync(saveAs: false);
            return true;
        }
        if (key == Key.O && modifiers.HasFlag(KeyModifiers.Control) && _fileOps is not null)
        {
            _ = _fileOps.OpenAsync();
            return true;
        }
        if (key == Key.N && modifiers.HasFlag(KeyModifiers.Control))
        {
            _onCreateNewCanvas?.Invoke();
            return true;
        }

        // Undo/Redo
        if (key == Key.Z && modifiers.HasFlag(KeyModifiers.Control))
        {
            Vm.UndoRedo.Undo();
            return true;
        }
        if (key == Key.Y && modifiers.HasFlag(KeyModifiers.Control))
        {
            Vm.UndoRedo.Redo();
            return true;
        }

        // Layer ordering
        if (key == Key.PageUp && modifiers.HasFlag(KeyModifiers.Control))
        {
            if (modifiers.HasFlag(KeyModifiers.Shift))
                Vm.BringSelectionToFrontCommand.Execute(null);
            else
                Vm.BringSelectionForwardCommand.Execute(null);
            return true;
        }
        if (key == Key.PageDown && modifiers.HasFlag(KeyModifiers.Control))
        {
            if (modifiers.HasFlag(KeyModifiers.Shift))
                Vm.SendSelectionToBackCommand.Execute(null);
            else
                Vm.SendSelectionBackwardCommand.Execute(null);
            return true;
        }

        // Command palette
        if (key == Key.K && modifiers.HasFlag(KeyModifiers.Control))
        {
            if (_commandPalette is null)
                return false;

            _commandPalette.Open();
            return true;
        }

        // Shortcuts help
        if (key == Key.F1)
        {
            if (_showShortcutsAction is not null)
                _showShortcutsAction();
            else if (_window is not null)
                new KeyboardShortcutsWindow().Show(_window);
            return true;
        }

        // Flow Version History (Ctrl+Shift+H)
        if (
            key == Key.H
            && modifiers.HasFlag(KeyModifiers.Control)
            && modifiers.HasFlag(KeyModifiers.Shift)
        )
        {
            Vm.FlowVersions.Open();
            return true;
        }

        // Save/Load local file history (Ctrl+Alt+H)
        if (
            key == Key.H
            && modifiers.HasFlag(KeyModifiers.Control)
            && modifiers.HasFlag(KeyModifiers.Alt)
        )
        {
            Vm.FileHistory.Open();
            return true;
        }

        // Connection manager
        if (
            key == Key.C
            && modifiers.HasFlag(KeyModifiers.Control)
            && modifiers.HasFlag(KeyModifiers.Shift)
        )
        {
            Vm.ConnectionManager.Open();
            return true;
        }

        // Canvas operations
        if (key == Key.L && modifiers.HasFlag(KeyModifiers.Control))
        {
            Vm.RunAutoLayout();
            _window?.FindControl<InfiniteCanvas>("TheCanvas")?.InvalidateWires();
            return true;
        }
        if (key == Key.G && modifiers.HasFlag(KeyModifiers.Control))
        {
            Vm.ToggleSnapCommand.Execute(null);
            return true;
        }

        // Explain Plan
        if (key == Key.F4)
        {
            Vm.ExplainPlan.Open();
            return true;
        }

        // Preview
        if (key == Key.F3)
        {
            Vm.DataPreview.Toggle();
            return true;
        }
        if (key == Key.F5)
        {
            return true;
        } // Handled by command palette

        // Delete selected nodes
        if ((key == Key.Delete || key == Key.Back) && modifiers == KeyModifiers.None)
        {
            Vm.DeleteSelected();
            return true;
        }

        // Zoom
        if (
            (key == Key.OemPlus || key == Key.Add)
            && modifiers.HasFlag(KeyModifiers.Control)
        )
        {
            Vm.ZoomInCommand.Execute(null);
            return true;
        }
        if (
            (key == Key.OemMinus || key == Key.Subtract)
            && modifiers.HasFlag(KeyModifiers.Control)
        )
        {
            Vm.ZoomOutCommand.Execute(null);
            return true;
        }
        if (
            (key == Key.D0 || key == Key.NumPad0)
            && modifiers.HasFlag(KeyModifiers.Control)
        )
        {
            Vm.ResetZoomCommand.Execute(null);
            return true;
        }

        return false;
    }

    private void OpenSearch()
    {
        if (_openSearchAction is not null)
        {
            _openSearchAction();
            return;
        }

        if (_window is null)
            return;

        InfiniteCanvas? canvas = _window.FindControl<InfiniteCanvas>("TheCanvas");
        Point ctr = canvas is not null
            ? new Point(canvas.Bounds.Width / 2, canvas.Bounds.Height / 2)
            : new Point(400, 300);
        Vm.SearchMenu.Open(ctr);
    }
}
