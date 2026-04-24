using Avalonia.Input;
using Avalonia.VisualTree;
using Avalonia.Controls;
using AkkornStudio.UI.ViewModels.ErDiagram;

namespace AkkornStudio.UI.Controls.ErDiagram;

public sealed partial class ErEntityControl : UserControl
{
    private static readonly CanvasViewportGesturePolicy GesturePolicy = CanvasViewportGesturePolicy.ErCanvasDefault;

    public ErEntityControl()
    {
        InitializeComponent();
    }

    private void Root_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        PointerPointProperties pointerProperties = e.GetCurrentPoint(this).Properties;
        bool isPanGesture = CanvasViewportGestureDecisions.IsPanGesture(
            GesturePolicy,
            pointerProperties,
            e.KeyModifiers);
        if (isPanGesture)
            return;

        if (DataContext is not ErEntityNodeViewModel entity)
            return;

        ErCanvasControl? canvasControl = this.FindAncestorOfType<ErCanvasControl>();
        if (canvasControl is null)
            return;

        if (canvasControl.DataContext is not ErCanvasViewModel canvas)
            return;

        canvas.SelectedEntity = entity;
        e.Handled = true;
    }
}
