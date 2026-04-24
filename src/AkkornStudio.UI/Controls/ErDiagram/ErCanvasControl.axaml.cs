using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia;
using AkkornStudio.UI.ViewModels.ErDiagram;
using System.ComponentModel;
using AkkornStudio.UI.Controls;

namespace AkkornStudio.UI.Controls.ErDiagram;

public sealed partial class ErCanvasControl : UserControl
{
    private readonly CanvasViewportController _viewportController = new();
    private ErCanvasViewModel? _observedCanvas;
    private bool _isMarqueeSelecting;
    private Point _marqueeStartCanvas;
    private Point _marqueeCurrentCanvas;

    public ErCanvasControl()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        LayoutUpdated += (_, _) => UpdateViewportState();
    }

    private void Edge_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is not ErCanvasViewModel canvas)
            return;

        PointerPointProperties pointerProperties = e.GetCurrentPoint(this).Properties;
        bool isPanGesture = pointerProperties.IsMiddleButtonPressed
            || pointerProperties.IsRightButtonPressed
            || (pointerProperties.IsLeftButtonPressed && e.KeyModifiers.HasFlag(KeyModifiers.Alt));
        if (isPanGesture)
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

        PointerPointProperties pointerProperties = e.GetCurrentPoint(this).Properties;
        bool isPanGesture = pointerProperties.IsMiddleButtonPressed
            || pointerProperties.IsRightButtonPressed
            || (pointerProperties.IsLeftButtonPressed && e.KeyModifiers.HasFlag(KeyModifiers.Alt));
        if (isPanGesture)
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

        Canvas? sceneContentCanvas = this.FindControl<Canvas>("SceneContentCanvas");
        if (sceneContentCanvas is not null)
            sceneContentCanvas.DataContext = _observedCanvas;

        Canvas? overlayCanvas = this.FindControl<Canvas>("SelectionMarquee")?.Parent as Canvas;
        if (overlayCanvas is not null)
            overlayCanvas.DataContext = _observedCanvas;

        ItemsControl? edgeItems = this.FindControl<ItemsControl>("EdgeItems");
        if (edgeItems is not null)
            edgeItems.ItemsSource = _observedCanvas?.Edges;

        ItemsControl? entityItems = this.FindControl<ItemsControl>("EntityItems");
        if (entityItems is not null)
            entityItems.ItemsSource = _observedCanvas?.Entities;

        if (_observedCanvas is not null)
            _observedCanvas.PropertyChanged += OnCanvasPropertyChanged;

        SyncTransform();
    }

    private void OnCanvasPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ErCanvasViewModel.FocusRequestVersion))
        {
            Dispatcher.UIThread.Post(CenterViewportOnFocusTarget, DispatcherPriority.Background);
            return;
        }

        if (e.PropertyName is nameof(ErCanvasViewModel.Zoom)
            or nameof(ErCanvasViewModel.ViewportX)
            or nameof(ErCanvasViewModel.ViewportY)
            or nameof(ErCanvasViewModel.SelectedEntity)
            or nameof(ErCanvasViewModel.SelectedEdge))
        {
            SyncTransform();
        }
    }

    private void CenterViewportOnFocusTarget()
    {
        if (_observedCanvas is null)
            return;

        UpdateViewportState();
        _observedCanvas.CenterViewportOnCanvasPoint(_observedCanvas.FocusTargetX, _observedCanvas.FocusTargetY);
        SyncTransform();
    }

    private void Viewport_PointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if (_observedCanvas is null || sender is not CanvasViewportSurface viewportSurface)
            return;

        _viewportController.ZoomAtPointer(_observedCanvas, viewportSurface, e);
        viewportSurface.SyncViewport();
        e.Handled = true;
    }

    private void Viewport_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_observedCanvas is null || sender is not CanvasViewportSurface viewportSurface)
            return;

        PointerPointProperties props = e.GetCurrentPoint(viewportSurface).Properties;
        bool isPanGesture = props.IsMiddleButtonPressed
            || props.IsRightButtonPressed
            || (props.IsLeftButtonPressed && e.KeyModifiers.HasFlag(KeyModifiers.Alt));

        if (!isPanGesture)
        {
            if (!props.IsLeftButtonPressed)
                return;

            Point screen = e.GetPosition(viewportSurface);
            _marqueeStartCanvas = _viewportController.ScreenToCanvas(_observedCanvas, screen);
            _marqueeCurrentCanvas = _marqueeStartCanvas;
            _isMarqueeSelecting = true;
            UpdateMarqueeVisual();
            e.Pointer.Capture(viewportSurface);
            e.Handled = true;
            return;
        }

        _viewportController.BeginPan(viewportSurface, e.Pointer, e.GetPosition(viewportSurface));
        e.Handled = true;
    }

    private void Viewport_PointerMoved(object? sender, PointerEventArgs e)
    {
        if (_observedCanvas is null || sender is not CanvasViewportSurface viewportSurface)
            return;

        if (!_viewportController.TryPan(_observedCanvas, viewportSurface, e))
        {
            if (!_isMarqueeSelecting)
                return;

            _marqueeCurrentCanvas = _viewportController.ScreenToCanvas(_observedCanvas, e.GetPosition(viewportSurface));
            UpdateMarqueeVisual();
            return;
        }

        viewportSurface.SyncViewport();
    }

    private void Viewport_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_viewportController.EndPan(e))
            return;

        if (!_isMarqueeSelecting || _observedCanvas is null || sender is not CanvasViewportSurface viewportSurface)
            return;

        _isMarqueeSelecting = false;
        Rect region = CreateCanvasRegion(_marqueeStartCanvas, _marqueeCurrentCanvas);
        CanvasMarqueeAdorner? marquee = this.FindControl<CanvasMarqueeAdorner>("SelectionMarquee");
        if (marquee is not null)
            marquee.SelectionRect = default;
        e.Pointer.Capture(null);

        if (region.Width < 8 || region.Height < 8)
        {
            _observedCanvas.ClearSelection();
            return;
        }

        _ = _observedCanvas.TrySelectEntityInRegion(region);
        viewportSurface.SyncViewport();
    }

    private void UpdateViewportState()
    {
        if (_observedCanvas is null)
            return;

        CanvasViewportSurface? viewportSurface = this.FindControl<CanvasViewportSurface>("ViewportSurface");
        if (viewportSurface is null)
            return;

        _observedCanvas.SetViewportSize(viewportSurface.Bounds.Width, viewportSurface.Bounds.Height);
        viewportSurface.SyncViewport();
    }

    private void SyncTransform()
    {
        this.FindControl<CanvasViewportSurface>("ViewportSurface")?.SyncViewport();
        UpdateFocusAdorner();
        UpdateMarqueeVisual();
    }

    private void UpdateFocusAdorner()
    {
        if (_observedCanvas is null)
            return;

        CanvasFocusAdorner? adorner = this.FindControl<CanvasFocusAdorner>("FocusOverlay");
        if (adorner is null)
            return;

        if (!_observedCanvas.TryGetSelectionFrame(12, out Rect frame))
        {
            adorner.FocusRect = default;
            return;
        }

        adorner.FocusRect = new Rect(
            frame.X * _observedCanvas.Zoom + _observedCanvas.PanOffset.X,
            frame.Y * _observedCanvas.Zoom + _observedCanvas.PanOffset.Y,
            frame.Width * _observedCanvas.Zoom,
            frame.Height * _observedCanvas.Zoom);
    }

    private void UpdateMarqueeVisual()
    {
        CanvasMarqueeAdorner? marquee = this.FindControl<CanvasMarqueeAdorner>("SelectionMarquee");
        if (marquee is null || _observedCanvas is null || !_isMarqueeSelecting)
        {
            if (marquee is not null)
                marquee.SelectionRect = default;

            return;
        }

        Rect region = CreateCanvasRegion(_marqueeStartCanvas, _marqueeCurrentCanvas);
        marquee.SelectionRect = new Rect(
            region.X * _observedCanvas.Zoom + _observedCanvas.PanOffset.X,
            region.Y * _observedCanvas.Zoom + _observedCanvas.PanOffset.Y,
            region.Width * _observedCanvas.Zoom,
            region.Height * _observedCanvas.Zoom);
    }

    private static Rect CreateCanvasRegion(Point start, Point end) =>
        new(
            Math.Min(start.X, end.X),
            Math.Min(start.Y, end.Y),
            Math.Abs(end.X - start.X),
            Math.Abs(end.Y - start.Y));
}
