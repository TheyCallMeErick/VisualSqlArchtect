using Avalonia.Input;
using Avalonia.VisualTree;
using Avalonia.Controls;
using AkkornStudio.UI.ViewModels.ErDiagram;

namespace AkkornStudio.UI.Controls.ErDiagram;

public sealed partial class ErEntityControl : UserControl
{
    public ErEntityControl()
    {
        InitializeComponent();
    }

    private void Root_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
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
