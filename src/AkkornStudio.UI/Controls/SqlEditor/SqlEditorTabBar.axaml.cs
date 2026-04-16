using Avalonia.Controls;
using Avalonia.Input;
using AkkornStudio.UI.ViewModels;

namespace AkkornStudio.UI.Controls.SqlEditor;

public partial class SqlEditorTabBar : UserControl
{
    private string? _dragSourceTabId;

    public SqlEditorTabBar()
    {
        InitializeComponent();
    }

    private void TabChip_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Control control)
            return;

        if (control.DataContext is not SqlEditorTabState tabState)
            return;

        if (!e.GetCurrentPoint(control).Properties.IsLeftButtonPressed)
            return;

        _dragSourceTabId = tabState.Id;
    }

    private void TabChip_OnPointerEntered(object? sender, PointerEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_dragSourceTabId))
            return;

        if (sender is not Control control)
            return;

        if (!e.GetCurrentPoint(control).Properties.IsLeftButtonPressed)
            return;

        if (control.DataContext is not SqlEditorTabState hoveredTab)
            return;

        if (DataContext is not SqlEditorViewModel vm)
            return;

        if (vm.ReorderTabs(_dragSourceTabId, hoveredTab.Id))
            _dragSourceTabId = hoveredTab.Id;
    }

    private void TabChip_OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _dragSourceTabId = null;
    }

    private void TabChip_OnPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        _dragSourceTabId = null;
    }
}
