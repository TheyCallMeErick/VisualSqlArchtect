using Avalonia;
using Avalonia.Controls;
using Material.Icons;
using VisualSqlArchitect.Nodes;
using VisualSqlArchitect.UI.Controls;
using VisualSqlArchitect.UI.Services.Localization;
using VisualSqlArchitect.UI.ViewModels;

namespace VisualSqlArchitect.UI.Services;

/// <summary>
/// Factory for creating command palette commands.
/// Centralizes 30+ command definitions with their shortcuts and metadata.
/// </summary>
public class CommandPaletteFactory(
    Window window,
    Func<CanvasViewModel> activeCanvasAccessor,
    Func<ShellViewModel> shellAccessor,
    FileOperationsService fileOps,
    ExportService export,
    PreviewService preview,
    Action? onCreateNewCanvas = null
)
{
    private readonly Window _window = window;
    private readonly Func<CanvasViewModel> _activeCanvasAccessor = activeCanvasAccessor;
    private readonly Func<ShellViewModel> _shellAccessor = shellAccessor;
    private readonly FileOperationsService _fileOps = fileOps;
    private readonly ExportService _export = export;
    private readonly PreviewService _preview = preview;
    private readonly Action? _onCreateNewCanvas = onCreateNewCanvas;
    private CanvasViewModel CurrentCanvas => _activeCanvasAccessor();
    private ShellViewModel CurrentShell => _shellAccessor();

    public IReadOnlyList<PaletteCommandItem> CreateAllCommands() =>
        [..CreateBasicCommands(), ..CreateTemplateCommands()];

    private List<PaletteCommandItem> CreateBasicCommands() =>
        [
            // ── Flow Version History ──────────────────────────────────────────
            new()
            {
                Name = LN("Flow Version History"),
                Description = LD("Create checkpoints, compare versions side-by-side and restore a previous canvas state"),
                Shortcut = "Ctrl+Shift+H",
                Icon = MaterialIconKind.ClipboardTextHistory,
                Tags = LTg("version history checkpoint diff restore snapshot compare undo flow"),
                Execute = () => CurrentCanvas.FlowVersions.Open(),
            },
            new()
            {
                Name = LN("File Save/Load History"),
                Description = LD("Open local file version history created on each save and restore previous saved snapshots"),
                Shortcut = "Ctrl+Alt+H",
                Icon = MaterialIconKind.History,
                Tags = LTg("file history save load backup versions restore local"),
                Execute = () => CurrentCanvas.FileHistory.Open(),
            },
            new()
            {
                Name = LN("Keyboard Shortcuts"),
                Description = LD("Open shortcut reference screen"),
                Shortcut = "F1",
                Icon = MaterialIconKind.Keyboard,
                Tags = LTg("help shortcuts hotkeys keyboard reference"),
                Execute = () => new KeyboardShortcutsWindow().Show(_window),
            },
            // ── Auto-Join ─────────────────────────────────────────────────────
            new()
            {
                Name = LN("Analyze All Joins"),
                Description = LD("Scan all table-source nodes on the canvas for possible join relationships based on FK conventions and naming patterns"),
                Shortcut = "",
                Icon = MaterialIconKind.AutoFix,
                Tags = LTg("join autojoin analyze suggest detect foreign key relationships heuristic"),
                Execute = () => CurrentCanvas.AnalyzeAllCanvasJoins(),
            },
            // ── SQL Importer ──────────────────────────────────────────────────
            new()
            {
                Name = LN("Import SQL to Graph"),
                Description = LD("Paste a SELECT statement and generate nodes automatically — FROM, JOIN, WHERE, LIMIT are supported"),
                Shortcut = "",
                Icon = MaterialIconKind.CodeBrackets,
                Tags = LTg("import sql paste convert graph reverse engineer query"),
                Execute = () => CurrentCanvas.SqlImporter.Open(),
            },
            // ── Snippets ──────────────────────────────────────────────────────
            new()
            {
                Name = LN("Save Selection as Snippet"),
                Description = LD("Save the selected nodes as a reusable snippet — insert it later via the node search menu (⇧A)"),
                Shortcut = "",
                Icon = MaterialIconKind.BookmarkPlus,
                Tags = LTg("snippet save selection reuse template favorite bookmark"),
                Execute = () =>
                {
                    var selected = CurrentCanvas.Nodes.Where(n => n.IsSelected).ToList();
                    if (selected.Count == 0)
                        return;
                    // Auto-name: first node title + count
                    string baseName = selected[0].Title;
                    string name = selected.Count == 1
                        ? baseName
                        : $"{baseName} +{selected.Count - 1}";
                    CurrentCanvas.SaveSelectionAsSnippet(name);
                },
            },
            new()
            {
                Name = LN("Edit Selected CTE"),
                Description = LD("Open isolated sub-canvas editor for the selected CTE Definition node"),
                Shortcut = "Ctrl+Alt+Enter",
                Icon = MaterialIconKind.FileTree,
                Tags = LTg("cte with recursive editor subgraph subcanvas isolate"),
                Execute = () => CurrentCanvas.EnterCteEditorCommand.Execute(null),
            },
            new()
            {
                Name = LN("Exit CTE Editor"),
                Description = LD("Apply CTE sub-canvas edits and return to the parent canvas"),
                Shortcut = "Esc",
                Icon = MaterialIconKind.ExitToApp,
                Tags = LTg("cte subcanvas exit apply back"),
                Execute = () => CurrentCanvas.ExitCteEditorCommand.Execute(null),
            },
            new()
            {
                Name = LN("Discard and Exit Editor"),
                Description = LD("Discard current sub-editor edits and return to the parent canvas"),
                Shortcut = "",
                Icon = MaterialIconKind.ExitRun,
                Tags = LTg("cte view subcanvas discard exit force"),
                Execute = () => CurrentCanvas.DiscardAndExitSubEditorCommand.Execute(null),
            },
            // ── Explain Plan ──────────────────────────────────────────────────
            new()
            {
                Name = LN("Explain Plan"),
                Description = LD("Inspect the query execution plan — see scan types, join strategies, and cost estimates"),
                Shortcut = "F4",
                Icon = MaterialIconKind.TableSearch,
                Tags = LTg("explain plan execution cost scan index join performance"),
                Execute = () => CurrentCanvas.ExplainPlan.Open(),
            },
            // ── Benchmark ─────────────────────────────────────────────────────
            new()
            {
                Name = LN("Run Query Benchmark"),
                Description = LD("Measure avg / median / p95 latency of the current SQL over N iterations"),
                Shortcut = "",
                Icon = MaterialIconKind.TimerOutline,
                Tags = LTg("benchmark performance latency timing profile measure speed"),
                Execute = () => CurrentCanvas.Benchmark.Open(),
            },
            // ── Connection ────────────────────────────────────────────────────
            new()
            {
                Name = LN("Manage Connections"),
                Description = LD("Open the connection manager to add, edit or switch database connections"),
                Shortcut = "Ctrl+Shift+C",
                Icon = MaterialIconKind.DatabaseSettings,
                Tags = LTg("connection database server host provider switch"),
                Execute = () => CurrentCanvas.ConnectionManager.Open(),
            },
            // ── File ──────────────────────────────────────────────────────────
            new()
            {
                Name = LN("New Canvas"),
                Description = LD("Clear canvas and start fresh"),
                Shortcut = "Ctrl+N",
                Icon = MaterialIconKind.FileOutline,
                Tags = LTg("reset clear blank"),
                Execute = () =>
                {
                    if (_onCreateNewCanvas is not null)
                    {
                        _onCreateNewCanvas();
                        return;
                    }

                    _window.DataContext = new CanvasViewModel();
                    if (_window.DataContext is CanvasViewModel currentVm)
                        _window.Title = currentVm.WindowTitle;
                },
            },
            new()
            {
                Name = LN("Open File"),
                Description = LD("Load a .vsaq canvas file"),
                Shortcut = "Ctrl+O",
                Icon = MaterialIconKind.FolderOpenOutline,
                Tags = LTg("load import vsaq"),
                Execute = async () => await _fileOps.OpenAsync(),
            },
            new()
            {
                Name = LN("Save"),
                Description = LD("Save current canvas"),
                Shortcut = "Ctrl+S",
                Icon = MaterialIconKind.ContentSave,
                Tags = LTg("persist write disk"),
                Execute = async () => await _fileOps.SaveAsync(saveAs: false),
            },
            new()
            {
                Name = LN("Save As"),
                Description = LD("Save canvas to a new file"),
                Shortcut = "Ctrl+Shift+S",
                Icon = MaterialIconKind.ContentSaveEdit,
                Tags = LTg("export persist copy"),
                Execute = async () => await _fileOps.SaveAsync(saveAs: true),
            },
            // ── Edit ──────────────────────────────────────────────────────────
            new()
            {
                Name = L("command.undo.name", "Undo"),
                Description = L("command.undo.description", "Undo last action"),
                Shortcut = "Ctrl+Z",
                Icon = MaterialIconKind.Undo,
                Tags = LTg("revert back history"),
                Execute = () => CurrentCanvas.UndoRedo.Undo(),
            },
            new()
            {
                Name = L("command.redo.name", "Redo"),
                Description = L("command.redo.description", "Redo last undone action"),
                Shortcut = "Ctrl+Y",
                Icon = MaterialIconKind.Redo,
                Tags = LTg("forward history"),
                Execute = () => CurrentCanvas.UndoRedo.Redo(),
            },
            new()
            {
                Name = LN("Select All"),
                Description = LD("Select all nodes on canvas"),
                Shortcut = "Ctrl+A",
                Icon = MaterialIconKind.SelectAll,
                Tags = LTg("highlight mark all nodes"),
                Execute = () => CurrentCanvas.SelectAllCommand.Execute(null),
            },
            new()
            {
                Name = LN("Deselect All"),
                Description = LD("Clear node selection"),
                Shortcut = "Esc",
                Icon = MaterialIconKind.SelectOff,
                Tags = LTg("clear selection"),
                Execute = () => CurrentCanvas.DeselectAllCommand.Execute(null),
            },
            new()
            {
                Name = LN("Delete Selected"),
                Description = LD("Delete the selected nodes"),
                Shortcut = "Del",
                Icon = MaterialIconKind.Delete,
                Tags = LTg("remove erase nodes"),
                Execute = () => CurrentCanvas.DeleteSelectedCommand.Execute(null),
            },
            // ── Canvas ────────────────────────────────────────────────────────
            new()
            {
                Name = L("command.addNode.name", "Add Node"),
                Description = L("command.addNode.description", "Open node search menu to add a node"),
                Shortcut = "Shift+A",
                Icon = MaterialIconKind.Plus,
                Tags = LTg("create insert search transform"),
                Execute = () => OpenSearch(),
            },
            new()
            {
                Name = L("command.bringForward.name", "Bring Forward"),
                Description = L("command.bringForward.description", "Move selected nodes one layer forward"),
                Shortcut = "Ctrl+PgUp",
                Icon = MaterialIconKind.ArrangeBringForward,
                Tags = LTg("layer z-order forward selected nodes"),
                Execute = () => CurrentCanvas.BringSelectionForwardCommand.Execute(null),
            },
            new()
            {
                Name = L("command.sendBackward.name", "Send Backward"),
                Description = L("command.sendBackward.description", "Move selected nodes one layer backward"),
                Shortcut = "Ctrl+PgDown",
                Icon = MaterialIconKind.ArrangeSendBackward,
                Tags = LTg("layer z-order backward selected nodes"),
                Execute = () => CurrentCanvas.SendSelectionBackwardCommand.Execute(null),
            },
            new()
            {
                Name = L("command.bringToFront.name", "Bring to Front"),
                Description = L("command.bringToFront.description", "Move selected nodes to top layer"),
                Shortcut = "Ctrl+Shift+PgUp",
                Icon = MaterialIconKind.ArrangeBringToFront,
                Tags = LTg("layer z-order front selected nodes"),
                Execute = () => CurrentCanvas.BringSelectionToFrontCommand.Execute(null),
            },
            new()
            {
                Name = L("command.sendToBack.name", "Send to Back"),
                Description = L("command.sendToBack.description", "Move selected nodes to bottom layer"),
                Shortcut = "Ctrl+Shift+PgDown",
                Icon = MaterialIconKind.ArrangeSendToBack,
                Tags = LTg("layer z-order back selected nodes"),
                Execute = () => CurrentCanvas.SendSelectionToBackCommand.Execute(null),
            },
            new()
            {
                Name = L("command.normalizeLayers.name", "Normalize Layers"),
                Description = L("command.normalizeLayers.description", "Compact node layer indices to a clean 0..N order"),
                Shortcut = "",
                Icon = MaterialIconKind.LayersTriple,
                Tags = LTg("layer z-order normalize compact"),
                Execute = () => CurrentCanvas.NormalizeLayersCommand.Execute(null),
            },
            new()
            {
                Name = LN("Zoom In"),
                Description = LD("Zoom into the canvas"),
                Shortcut = "Ctrl++",
                Icon = MaterialIconKind.MagnifyPlus,
                Tags = LTg("magnify enlarge"),
                Execute = () => CurrentCanvas.ZoomInCommand.Execute(null),
            },
            new()
            {
                Name = LN("Zoom Out"),
                Description = LD("Zoom out of the canvas"),
                Shortcut = "Ctrl+-",
                Icon = MaterialIconKind.MagnifyMinus,
                Tags = LTg("shrink reduce"),
                Execute = () => CurrentCanvas.ZoomOutCommand.Execute(null),
            },
            new()
            {
                Name = LN("Fit to Screen"),
                Description = LD("Fit all nodes into the visible area"),
                Shortcut = "",
                Icon = MaterialIconKind.FitToPage,
                Tags = LTg("auto layout view reset zoom"),
                Execute = () => CurrentCanvas.FitToScreenCommand.Execute(null),
            },
            new()
            {
                Name = LN("Reset Viewport"),
                Description = LD("Reset zoom and pan to default"),
                Shortcut = "Ctrl+0",
                Icon = MaterialIconKind.MagnifyRemoveOutline,
                Tags = LTg("100 percent restore zoom pan viewport"),
                Execute = () => CurrentCanvas.ResetZoomCommand.Execute(null),
            },
            // ── Query / Preview ───────────────────────────────────────────────
            new()
            {
                Name = LN("Toggle Preview"),
                Description = LD("Open output preview modal for the active mode"),
                Shortcut = "F3",
                Icon = MaterialIconKind.TableEye,
                Tags = LTg("data results table panel"),
                Execute = OpenOutputPreviewModal,
            },
            new()
            {
                Name = LN("Run Preview"),
                Description = LD("Execute the current query in preview"),
                Shortcut = "F5",
                Icon = MaterialIconKind.Play,
                Tags = LTg("execute run sql query results"),
                Execute = async () =>
                {
                    if (!CurrentCanvas.HasErrors && !CurrentCanvas.LiveSql.IsMutatingCommand)
                        await _preview.RunPreviewAsync();
                },
            },
            // ── Cleanup / Quality ─────────────────────────────────────────────
            new()
            {
                Name = LN("Auto Layout"),
                Description = LD("Arrange nodes into logical columns automatically"),
                Shortcut = "Ctrl+L",
                Icon = MaterialIconKind.FormatHorizontalAlignCenter,
                Tags = LTg("layout arrange columns auto organize readability"),
                Execute = () =>
                {
                    CurrentCanvas.AutoLayoutCommand.Execute(null);
                    _window.FindControl<InfiniteCanvas>("TheCanvas")?.InvalidateWires();
                },
            },
            new()
            {
                Name = LN("Cleanup Orphans"),
                Description = LD("Remove all nodes not connected to output"),
                Shortcut = "",
                Icon = MaterialIconKind.VectorUnion,
                Tags = LTg("orphan unused disconnected clean delete nodes"),
                Execute = () => CurrentCanvas.CleanupOrphansCommand.Execute(null),
            },
            new()
            {
                Name = LN("Auto-Fix Naming"),
                Description = LD("Convert aliases to the convention configured in project settings"),
                Shortcut = "",
                Icon = MaterialIconKind.AutoFix,
                Tags = LTg("rename alias fix naming convention"),
                Execute = () => CurrentCanvas.AutoFixNamingCommand.Execute(null),
            },
            // ── Snap / Alignment ──────────────────────────────────────────────
            new()
            {
                Name = LN("Toggle Snap to Grid"),
                Description = LD("Snap node positions to 16px grid (Ctrl+G)"),
                Shortcut = "Ctrl+G",
                Icon = MaterialIconKind.GridLarge,
                Tags = LTg("snap grid align precision position"),
                Execute = () => CurrentCanvas.ToggleSnapCommand.Execute(null),
            },
            new()
            {
                Name = LN("Align Left"),
                Description = LD("Align selected nodes to the leftmost edge"),
                Shortcut = "",
                Icon = MaterialIconKind.AlignHorizontalLeft,
                Tags = LTg("align left edge selection nodes"),
                Execute = () => CurrentCanvas.AlignLeftCommand.Execute(null),
            },
            new()
            {
                Name = LN("Align Right"),
                Description = LD("Align selected nodes to the rightmost edge"),
                Shortcut = "",
                Icon = MaterialIconKind.AlignHorizontalRight,
                Tags = LTg("align right edge selection nodes"),
                Execute = () => CurrentCanvas.AlignRightCommand.Execute(null),
            },
            new()
            {
                Name = LN("Align Top"),
                Description = LD("Align selected nodes to the topmost edge"),
                Shortcut = "",
                Icon = MaterialIconKind.AlignVerticalTop,
                Tags = LTg("align top edge selection nodes"),
                Execute = () => CurrentCanvas.AlignTopCommand.Execute(null),
            },
            new()
            {
                Name = LN("Align Bottom"),
                Description = LD("Align selected nodes to the bottom edge"),
                Shortcut = "",
                Icon = MaterialIconKind.AlignVerticalBottom,
                Tags = LTg("align bottom edge selection nodes"),
                Execute = () => CurrentCanvas.AlignBottomCommand.Execute(null),
            },
            new()
            {
                Name = LN("Center Horizontally"),
                Description = LD("Centre selected nodes on a horizontal axis"),
                Shortcut = "",
                Icon = MaterialIconKind.AlignHorizontalCenter,
                Tags = LTg("align center middle horizontal nodes"),
                Execute = () => CurrentCanvas.AlignCenterHCommand.Execute(null),
            },
            new()
            {
                Name = LN("Center Vertically"),
                Description = LD("Centre selected nodes on a vertical axis"),
                Shortcut = "",
                Icon = MaterialIconKind.AlignVerticalCenter,
                Tags = LTg("align center middle vertical nodes"),
                Execute = () => CurrentCanvas.AlignCenterVCommand.Execute(null),
            },
            new()
            {
                Name = LN("Distribute Horizontally"),
                Description = LD("Spread selected nodes with equal horizontal spacing"),
                Shortcut = "",
                Icon = MaterialIconKind.DistributeHorizontalCenter,
                Tags = LTg("distribute space equal horizontal nodes"),
                Execute = () => CurrentCanvas.DistributeHCommand.Execute(null),
            },
            new()
            {
                Name = LN("Distribute Vertically"),
                Description = LD("Spread selected nodes with equal vertical spacing"),
                Shortcut = "",
                Icon = MaterialIconKind.DistributeVerticalCenter,
                Tags = LTg("distribute space equal vertical nodes"),
                Execute = () => CurrentCanvas.DistributeVCommand.Execute(null),
            },
            // ── Export ────────────────────────────────────────────────────────
            new()
            {
                Name = LN("Export Documentation"),
                Description = LD("Save Markdown documentation of the current flow"),
                Shortcut = "",
                Icon = MaterialIconKind.FileDocument,
                Tags = LTg("export markdown doc documentation flow save md"),
                Execute = async () => await _export.RunExportDocumentationAsync(),
            },
            new()
            {
                Name = LN("Export HTML"),
                Description = LD("Generate HTML file from the first HTML Export node"),
                Shortcut = "",
                Icon = MaterialIconKind.LanguageHtml5,
                Tags = LTg("export html file output report save"),
                Execute = async () =>
                    await _export.RunExportWithDialogAsync(
                        NodeType.HtmlExport,
                        L("export.fileType.html", "HTML Files"),
                        "html"
                    ),
            },
            new()
            {
                Name = LN("Export JSON"),
                Description = LD("Generate JSON file from the first JSON Export node"),
                Shortcut = "",
                Icon = MaterialIconKind.CodeJson,
                Tags = LTg("export json file output save"),
                Execute = async () =>
                    await _export.RunExportWithDialogAsync(
                        NodeType.JsonExport,
                        L("export.fileType.json", "JSON Files"),
                        "json"
                    ),
            },
            new()
            {
                Name = LN("Export CSV"),
                Description = LD("Generate CSV file from the first CSV Export node"),
                Shortcut = "",
                Icon = MaterialIconKind.FileDelimited,
                Tags = LTg("export csv file tabular output save"),
                Execute = async () =>
                    await _export.RunExportWithDialogAsync(
                        NodeType.CsvExport,
                        L("export.fileType.csv", "CSV Files"),
                        "csv"
                    ),
            },
            new()
            {
                Name = LN("Export Excel"),
                Description = LD("Generate XLSX workbook from the first Excel Export node"),
                Shortcut = "",
                Icon = MaterialIconKind.MicrosoftExcel,
                Tags = LTg("export excel xlsx file tabular output spreadsheet save"),
                Execute = async () =>
                    await _export.RunExportWithDialogAsync(
                        NodeType.ExcelExport,
                        L("export.fileType.excel", "Excel Files"),
                        "xlsx"
                    ),
            },
        ];

    private void OpenOutputPreviewModal()
    {
        ShellViewModel shell = CurrentShell;

        if (shell.IsDdlModeActive)
        {
            CanvasViewModel ddlCanvas = shell.EnsureDdlCanvas();
            ddlCanvas.LiveDdl?.Recompile();
            if (ddlCanvas.LiveDdl is LiveDdlBarViewModel liveDdl)
                shell.OutputPreview.OpenForDdl(ddlCanvas, liveDdl, ddlCanvas.Provider.ToString());
            return;
        }

        CurrentCanvas.DataPreview.IsVisible = true;
        shell.OutputPreview.OpenForQuery(CurrentCanvas);
    }

    private List<PaletteCommandItem> CreateTemplateCommands() =>
        [
            .. QueryTemplateLibrary.All.Select(t => new PaletteCommandItem
            {
                Name = string.Format(L("commandPalette.templatePrefix", "Template: {0}"), t.Name),
                Description = t.Description,
                Icon = MaterialIconKind.ViewGrid,
                Tags = $"template starter query {t.Category.ToLowerInvariant()} {t.Tags}",
                Execute = () =>
                {
                    CurrentCanvas.LoadTemplate(t);
                    _window.FindControl<InfiniteCanvas>("TheCanvas")?.InvalidateWires();
                },
            }),
        ];

    private void OpenSearch()
    {
        InfiniteCanvas? canvas = _window.FindControl<InfiniteCanvas>("TheCanvas");
        Point ctr = canvas is not null
            ? new Point(canvas.Bounds.Width / 2, canvas.Bounds.Height / 2)
            : new Point(400, 300);
        CurrentCanvas.SearchMenu.Open(ctr);
    }

    private static string L(string key, string fallback)
    {
        string value = LocalizationService.Instance[key];
        return string.Equals(value, key, StringComparison.Ordinal) ? fallback : value;
    }

    private static string LN(string fallback) =>
        L($"commandPalette.name.{Slugify(fallback)}", fallback);

    private static string LD(string fallback) =>
        L($"commandPalette.description.{Slugify(fallback)}", fallback);

    private static string LTg(string fallback) =>
        L($"commandPalette.tags.{Slugify(fallback)}", fallback);

    private static string Slugify(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return "empty";

        char[] buffer = new char[text.Length];
        int idx = 0;
        bool lastWasUnderscore = false;
        foreach (char ch in text.ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(ch))
            {
                buffer[idx++] = ch;
                lastWasUnderscore = false;
                continue;
            }

            if (!lastWasUnderscore)
            {
                buffer[idx++] = '_';
                lastWasUnderscore = true;
            }
        }

        string raw = new string(buffer, 0, idx).Trim('_');
        return string.IsNullOrWhiteSpace(raw) ? "value" : raw;
    }
}
