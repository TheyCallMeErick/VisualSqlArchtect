using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Input;
using System.Collections.Generic;
using System.Linq;
using VisualSqlArchitect.UI.Services.Localization;

namespace VisualSqlArchitect.UI.Controls;

public sealed class KeyboardShortcutsWindow : Window
{
    private sealed record ShortcutItem(string Section, string Key, string Action);

    private readonly List<ShortcutItem> _allShortcuts;

    private TextBox? _searchBox;
    private TextBlock? _resultInfo;
    private StackPanel? _sectionsHost;

    public KeyboardShortcutsWindow()
    {
        _allShortcuts = BuildShortcuts();
        Title = L("shortcuts.windowTitle", "Keyboard Shortcuts");
        Width = 760;
        Height = 700;
        MinWidth = 620;
        MinHeight = 520;
        Background = new SolidColorBrush(Color.Parse("#0D0F14"));
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        KeyDown += OnKeyDown;

        Content = new Border
        {
            Padding = new Thickness(16),
            Child = BuildContent(),
        };
    }

    private Control BuildContent()
    {
        var root = new StackPanel
        {
            Spacing = 14,
        };

        root.Children.Add(new TextBlock
        {
            Text = L("shortcuts.headerTitle", "Visual SQL Architect — Shortcuts"),
            FontSize = 20,
            FontWeight = FontWeight.SemiBold,
            Foreground = new SolidColorBrush(Color.Parse("#E8EAED")),
        });

        root.Children.Add(new TextBlock
        {
            Text = L("shortcuts.headerHint", "Tip: use Ctrl+K to open the Command Palette and search commands."),
            FontSize = 12,
            Foreground = new SolidColorBrush(Color.Parse("#8B95A8")),
        });

        _searchBox = new TextBox
        {
            Watermark = L("shortcuts.filterWatermark", "Filter shortcuts by key or action..."),
            Background = new SolidColorBrush(Color.Parse("#101521")),
            BorderBrush = new SolidColorBrush(Color.Parse("#1E2335")),
            BorderThickness = new Thickness(1),
            Foreground = new SolidColorBrush(Color.Parse("#E8EAED")),
        };
        _searchBox.TextChanged += (_, _) => RenderSections(_searchBox.Text);
        root.Children.Add(_searchBox);

        _resultInfo = new TextBlock
        {
            FontSize = 11,
            Foreground = new SolidColorBrush(Color.Parse("#8B95A8")),
        };
        root.Children.Add(_resultInfo);

        _sectionsHost = new StackPanel { Spacing = 10 };

        RenderSections();

        return new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = new StackPanel
            {
                Spacing = 10,
                Children =
                {
                    root,
                    _sectionsHost,
                },
            },
        };
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            if (!string.IsNullOrWhiteSpace(_searchBox?.Text))
            {
                _searchBox!.Text = string.Empty;
                e.Handled = true;
                return;
            }

            Close();
            e.Handled = true;
        }
    }

    private void RenderSections(string? filter = null)
    {
        if (_sectionsHost is null)
            return;

        string f = (filter ?? string.Empty).Trim();
        IEnumerable<ShortcutItem> rows = _allShortcuts;

        if (!string.IsNullOrWhiteSpace(f))
        {
            rows = rows.Where(x =>
                x.Key.Contains(f, StringComparison.OrdinalIgnoreCase)
                || x.Action.Contains(f, StringComparison.OrdinalIgnoreCase)
                || x.Section.Contains(f, StringComparison.OrdinalIgnoreCase));
        }

        List<ShortcutItem> filtered = rows.ToList();
        _sectionsHost.Children.Clear();

        if (_resultInfo is not null)
            _resultInfo.Text = string.IsNullOrWhiteSpace(f)
                ? string.Format(L("shortcuts.resultCount", "{0} shortcuts"), _allShortcuts.Count)
                : string.Format(L("shortcuts.resultFilter", "{0} result(s) for \"{1}\""), filtered.Count, f);

        if (filtered.Count == 0)
        {
            _sectionsHost.Children.Add(new TextBlock
            {
                Text = L("shortcuts.noneFound", "No shortcuts found."),
                Foreground = new SolidColorBrush(Color.Parse("#8B95A8")),
            });
            return;
        }

        foreach (IGrouping<string, ShortcutItem> group in filtered.GroupBy(x => x.Section))
            _sectionsHost.Children.Add(Section(group.Key, [.. group.Select(x => (x.Key, x.Action))]));
    }

    private static Border Section(string title, params (string Key, string Action)[] rows)
    {
        var list = new StackPanel { Spacing = 8 };
        list.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 14,
            FontWeight = FontWeight.SemiBold,
            Foreground = new SolidColorBrush(Color.Parse("#60A5FA")),
        });

        foreach ((string key, string action) in rows)
        {
            var row = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("190,*"),
            };

            var keyBorder = new Border
            {
                Background = new SolidColorBrush(Color.Parse("#171B26")),
                BorderBrush = new SolidColorBrush(Color.Parse("#252C3F")),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(8, 4),
                Child = new TextBlock
                {
                    Text = key,
                    FontFamily = new FontFamily("JetBrains Mono,IBM Plex Mono,Cascadia Code,Consolas,monospace"),
                    FontSize = 12,
                    Foreground = new SolidColorBrush(Color.Parse("#E8EAED")),
                },
            };
            Grid.SetColumn(keyBorder, 0);
            row.Children.Add(keyBorder);

            var actionText = new TextBlock
            {
                Margin = new Thickness(10, 4, 0, 0),
                Text = action,
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.Parse("#C8D0DC")),
            };
            Grid.SetColumn(actionText, 1);
            row.Children.Add(actionText);

            list.Children.Add(row);
        }

        return new Border
        {
            Background = new SolidColorBrush(Color.Parse("#101521")),
            BorderBrush = new SolidColorBrush(Color.Parse("#1E2335")),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(12),
            Child = list,
        };
    }

    private List<ShortcutItem> BuildShortcuts()
    {
        string fileGeneral = L("shortcuts.section.fileGeneral", "File and General");
        string editing = L("shortcuts.section.editing", "Editing");
        string canvasNav = L("shortcuts.section.canvasNavigation", "Canvas and Navigation");
        string zoomPan = L("shortcuts.section.zoomPanPrecision", "Zoom, pan and precision");
        string previewInspect = L("shortcuts.section.previewInspection", "Preview and Inspection");

        return
        [
            new(fileGeneral, "F1", L("shortcuts.action.openShortcutScreen", "Open this shortcuts screen")),
            new(fileGeneral, "Ctrl+N", L("shortcuts.action.newCanvas", "New canvas")),
            new(fileGeneral, "Ctrl+O", L("shortcuts.action.openFile", "Open file")),
            new(fileGeneral, "Ctrl+S", L("shortcuts.action.save", "Save")),
            new(fileGeneral, "Ctrl+Shift+S", L("shortcuts.action.saveAs", "Save as")),
            new(fileGeneral, "Ctrl+K", L("shortcuts.action.commandPalette", "Command Palette")),

            new(editing, "Ctrl+Z", L("shortcuts.action.undo", "Undo")),
            new(editing, "Ctrl+Y", L("shortcuts.action.redo", "Redo")),
            new(editing, "Ctrl+A", L("shortcuts.action.selectAll", "Select all")),
            new(editing, L("shortcuts.key.deleteOrBackspace", "Del or Backspace"), L("shortcuts.action.deleteSelection", "Delete selection")),
            new(editing, "Esc", L("shortcuts.action.closeOverlayCancel", "Close overlays / cancel actions")),

            new(canvasNav, "Shift+A", L("shortcuts.action.openNodeSearch", "Open node search")),
            new(canvasNav, "Ctrl+F", L("shortcuts.action.openNodeSearch", "Open node search")),
            new(canvasNav, "Ctrl+0", L("shortcuts.action.resetViewport", "Reset viewport")),
            new(canvasNav, "F", L("shortcuts.action.centerSelection", "Center selection")),
            new(canvasNav, "Shift+F", L("shortcuts.action.fitSelection", "Fit selection")),
            new(canvasNav, "Ctrl+L", L("shortcuts.action.autoLayout", "Auto Layout")),
            new(canvasNav, "Ctrl+G", L("shortcuts.action.toggleSnapToGrid", "Toggle Snap to Grid")),
            new(canvasNav, "Ctrl+PgUp", L("shortcuts.action.bringForward", "Bring Forward")),
            new(canvasNav, "Ctrl+PgDown", L("shortcuts.action.sendBackward", "Send Backward")),
            new(canvasNav, "Ctrl+Shift+PgUp", L("shortcuts.action.bringToFront", "Bring to Front")),
            new(canvasNav, "Ctrl+Shift+PgDown", L("shortcuts.action.sendToBack", "Send to Back")),

            new(zoomPan, "Ctrl++ / Ctrl+-", L("shortcuts.action.zoomInOut", "Zoom in / out")),
            new(zoomPan, L("shortcuts.key.middleDrag", "Middle mouse + drag"), L("shortcuts.action.pan", "Pan")),
            new(zoomPan, L("shortcuts.key.rightDrag", "Right mouse + drag"), L("shortcuts.action.pan", "Pan")),
            new(zoomPan, L("shortcuts.key.spaceDrag", "Space + drag"), L("shortcuts.action.temporaryPan", "Temporary pan")),
            new(zoomPan, L("shortcuts.key.altLeftDrag", "Alt + left drag"), L("shortcuts.action.alternatePan", "Alternate pan")),
            new(zoomPan, L("shortcuts.key.arrows", "Arrows"), L("shortcuts.action.fineNudge", "Fine nudge selection")),
            new(zoomPan, L("shortcuts.key.shiftArrows", "Shift + Arrows"), L("shortcuts.action.fastNudge", "Fast nudge")),

            new(previewInspect, "F3", L("shortcuts.action.togglePreview", "Toggle data preview")),
            new(previewInspect, "F4", L("shortcuts.action.explainPlan", "Explain plan")),
            new(previewInspect, "F5", L("shortcuts.action.runPreview", "Run preview")),
            new(previewInspect, "Ctrl+Shift+C", L("shortcuts.action.connectionManager", "Connection manager")),
            new(previewInspect, "Ctrl+Shift+H", L("shortcuts.action.flowVersionHistory", "Flow version history")),
        ];
    }

    private static string L(string key, string fallback)
    {
        string value = LocalizationService.Instance[key];
        return string.Equals(value, key, StringComparison.Ordinal) ? fallback : value;
    }
}
