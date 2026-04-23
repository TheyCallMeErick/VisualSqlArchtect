using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia;
using AkkornStudio.UI.ViewModels.ErDiagram;
using System.ComponentModel;

namespace AkkornStudio.UI.Controls.ErDiagram;

public sealed partial class ErCanvasControl : UserControl
{
    private ErCanvasViewModel? _observedCanvas;

    public ErCanvasControl()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void Edge_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is not ErCanvasViewModel canvas)
            return;

        if (sender is not Control control || control.DataContext is not ErRelationEdgeViewModel edge)
            return;

        canvas.SelectedEdge = edge;
        e.Handled = true;
    }

    private void CanvasBackground_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.Source is not Canvas)
            return;

        if (DataContext is not ErCanvasViewModel canvas)
            return;

        canvas.ClearSelection();
        e.Handled = true;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_observedCanvas is not null)
            _observedCanvas.PropertyChanged -= OnCanvasPropertyChanged;

        _observedCanvas = DataContext as ErCanvasViewModel;

        if (_observedCanvas is not null)
            _observedCanvas.PropertyChanged += OnCanvasPropertyChanged;
    }

    private void OnCanvasPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(ErCanvasViewModel.FocusRequestVersion))
            return;

        Dispatcher.UIThread.Post(CenterViewportOnFocusTarget, DispatcherPriority.Background);
    }

    private void CenterViewportOnFocusTarget()
    {
        if (_observedCanvas is null)
            return;

        ScrollViewer? scrollViewer = this.FindControl<ScrollViewer>("CanvasScrollViewer");
        if (scrollViewer is null)
            return;

        Size viewport = scrollViewer.Viewport;
        Size extent = scrollViewer.Extent;
        if (viewport.Width <= 0 || viewport.Height <= 0)
            return;

        double targetX = Math.Clamp(_observedCanvas.FocusTargetX - (viewport.Width / 2d), 0d, Math.Max(0d, extent.Width - viewport.Width));
        double targetY = Math.Clamp(_observedCanvas.FocusTargetY - (viewport.Height / 2d), 0d, Math.Max(0d, extent.Height - viewport.Height));

        scrollViewer.Offset = new Vector(targetX, targetY);
        _observedCanvas.ViewportX = targetX;
        _observedCanvas.ViewportY = targetY;
    }
}
