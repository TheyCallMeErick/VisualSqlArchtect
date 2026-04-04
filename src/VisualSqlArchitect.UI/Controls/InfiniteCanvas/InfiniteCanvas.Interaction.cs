using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using VisualSqlArchitect.Nodes;
using VisualSqlArchitect.UI.Services.Localization;
using VisualSqlArchitect.UI.ViewModels;

namespace VisualSqlArchitect.UI.Controls;

/// <summary>
/// Interaction responsibilities extracted from InfiniteCanvas:
/// pointer/keyboard gestures, context menu, rubber-band visuals and alignment guides.
/// </summary>
public sealed partial class InfiniteCanvas
{
    // Note: In Avalonia 11+, Panel.Render() is sealed and cannot be overridden.
    // Rubber band selection rectangle is drawn as a visual element instead.
    private Border? _rubberBandRect;

    private const double GuideThreshold = 8.0; // canvas units — how close to snap a guide
    private const double DefaultNodeH = 130;

    private bool TryDeleteWire(ConnectionViewModel? wire)
    {
        if (ViewModel is null || wire is null)
            return false;

        ClearHoverHighlights();
        _wires.AddRemovalFlash(wire);
        ViewModel.DeleteConnection(wire);
        RequestWireSync();
        _wires.InvalidateVisual();
        return true;
    }

    private void OnPinPressed(object? s, (PinViewModel Pin, Point Canvas) a)
    {
        _pinDrag?.BeginDrag(a.Pin, a.Pin.AbsolutePosition);
        _wires.PendingConnection = _pinDrag?.LiveWire;
        Log($">>> PIN DRAG STARTED: Pin='{a.Pin.Owner.Title}.{a.Pin.Name}'");
    }

    private void OnWheel(object? s, PointerWheelEventArgs e)
    {
        if (ViewModel is null)
            return;
        double oldZoom = _zoom;
        Point oldPan = _panOffset;

        Point screen = e.GetPosition(this);
        double factor = e.Delta.Y > 0 ? 1.10 : 0.91;

        // Nodify-style approach: update viewport from a single source of truth (ViewModel),
        // then sync once to the visual transform.
        _isApplyingViewportFromCanvas = true;
        try
        {
            ViewModel.ZoomToward(screen, factor);
        }
        finally
        {
            _isApplyingViewportFromCanvas = false;
        }

        SyncTransform();
        SyncWires();
        e.Handled = true;
        Log($"    ZOOM: {oldZoom:F2} -> {_zoom:F2}, Pan: {oldPan} -> {_panOffset}, Delta={e.Delta.Y}");
    }

    private void OnPressed(object? s, PointerPressedEventArgs e)
    {
        Focus();
        PointerPointProperties props = e.GetCurrentPoint(this).Properties;
        Point screen = e.GetPosition(this);

        if (
            props.IsMiddleButtonPressed
            || (props.IsLeftButtonPressed && _isSpacePanArmed)
            || (props.IsLeftButtonPressed && e.KeyModifiers.HasFlag(KeyModifiers.Alt))
        )
        {
            ClearHoverHighlights();
            Log($">>> PAN STARTED: Screen={screen}, PanOffset={_panOffset}, Zoom={_zoom}");
            _isPanning = true;
            _panStart = screen; // Store screen position, not difference
            _startPanOffset = _panOffset; // NodifyM pattern: capture pan offset at start
            e.Pointer.Capture(this);
            Cursor = new Cursor(StandardCursorType.SizeAll);
            e.Handled = true;
            return;
        }

        // Nodify-style usability: right-drag pans, right-click opens context menu.
        if (props.IsRightButtonPressed)
        {
            _contextMenuPending = true;
            _contextMenuPressScreen = screen;
            e.Pointer.Capture(this);
            e.Handled = true;
            return;
        }

        if (props.IsLeftButtonPressed)
        {
            Point canvas = ScreenToCanvas(screen);
            _rubberStart = canvas;
            _rubberCurrent = canvas;

            // Fast delete gesture for wires: Ctrl+Click on a wire removes it.
            if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
            {
                ConnectionViewModel? wireUnderPointer = _wires.HitTestWire(canvas, tolerance: 8);
                if (TryDeleteWire(wireUnderPointer))
                {
                    e.Handled = true;
                    return;
                }
            }

            // If a NodeControl already fired PinPressed (which called BeginDrag via OnPinPressed),
            // the event bubbled here without e.Handled — just capture the pointer so that
            // move and release events are owned by the canvas for the full drag gesture.
            if (_pinDrag?.IsDragging == true)
            {
                e.Pointer.Capture(this);
                e.Handled = true;
                return;
            }
            PinViewModel? pin = _pinDrag?.HitTestPin(canvas);
            if (pin is not null)
            {
                _pinDrag!.BeginDrag(pin, pin.AbsolutePosition);
                _wires.PendingConnection = _pinDrag.LiveWire;
                e.Pointer.Capture(this);
                e.Handled = true;
                return;
            }

            // Rubber band intentionally disabled (temporary workaround for critical wire issue).
            _isRubberBanding = false;
            UpdateRubberBandVisual();
            ViewModel?.DeselectAll();
            e.Handled = true;
            return;
        }
    }

    private void OnMoved(object? s, PointerEventArgs e)
    {
        Point screen = e.GetPosition(this);
        Point canvas = ScreenToCanvas(screen);

        if (_contextMenuPending && !_isPanning)
        {
            Vector d = screen - _contextMenuPressScreen;
            if (d.X * d.X + d.Y * d.Y >= ContextPanStartThreshold * ContextPanStartThreshold)
            {
                _contextMenuPending = false;
                _isPanning = true;
                _panStart = screen;
                _startPanOffset = _panOffset;
                Cursor = new Cursor(StandardCursorType.SizeAll);
                Log($">>> PAN STARTED (right-drag): Screen={screen}, PanOffset={_panOffset}, Zoom={_zoom}");
            }
            else
            {
                return;
            }
        }

        if (_isPanning)
        {
            ClearHoverHighlights();
            Point delta = screen - _panStart;
            _panOffset = _panOffset + (Vector)delta;

            Log($"    PAN MOVING: Screen={screen}, Delta={delta}, NewPanOffset={_panOffset}, Zoom={_zoom}");

            // Force immediate visual update during pan (don't wait for layout pass)
            _scene.RenderTransform = new TransformGroup
            {
                Children =
                [
                    new ScaleTransform(_zoom, _zoom),
                    new TranslateTransform(_panOffset.X, _panOffset.Y),
                ],
            };
            _grid.PanOffset = _panOffset;
            _grid.InvalidateVisual();

            _panStart = screen; // Update for next frame
            return;
        }
        if (_pinDrag?.IsDragging == true)
        {
            ClearHoverHighlights();
            _pinDrag.UpdateDrag(canvas);
            _wires.PendingConnection = _pinDrag.LiveWire;
            _wires.InvalidateVisual();
            return;
        }
        if (RubberBandEnabled && _isRubberBanding)
        {
            _rubberCurrent = canvas;
            UpdateRubberBand();
            UpdateRubberBandVisual();

            // Ensure wires stay visible during rubber band drag
            _wires.InvalidateVisual();

            // Keep wires on top as nodes are selected
            EnsureWiresOnTop();
        }

        // Always ensure wires are on top after any movement
        if (!_isPanning && !(_pinDrag?.IsDragging == true))
        {
            EnsureWiresOnTop();
            UpdateHoverHighlights(canvas);
        }
    }

    private void OnReleased(object? s, PointerReleasedEventArgs e)
    {
        Point screen = e.GetPosition(this);
        Point canvas = ScreenToCanvas(e.GetPosition(this));

        if (_contextMenuPending)
        {
            _contextMenuPending = false;
            e.Pointer.Capture(null);
            ShowContextMenu(screen);
            return;
        }

        if (_isPanning)
        {
            Log($"<<< PAN COMPLETED: FinalPanOffset={_panOffset}, Zoom={_zoom}");
            // Final sync to ViewModel to ensure persistence
            if (ViewModel is not null)
            {
                ViewModel.PanOffset = _panOffset;
                Log($"    Synced to ViewModel: PanOffset={_panOffset}");
            }
            _isPanning = false;
            e.Pointer.Capture(null);
            Cursor = _isSpacePanArmed ? new Cursor(StandardCursorType.Hand) : Cursor.Default;
            return;
        }
        if (_pinDrag?.IsDragging == true)
        {
            _pinDrag.EndDrag(canvas);
            _wires.PendingConnection = null;
            _wires.InvalidateVisual();
            e.Pointer.Capture(null);
            return;
        }
        if (RubberBandEnabled && _isRubberBanding)
        {
            Log($"<<< RUBBER BAND COMPLETED: Canvas={canvas}");
            _isRubberBanding = false;
            UpdateRubberBand();
            UpdateRubberBandVisual();

            // Ensure wires are fully synced and visible after rubber band selection
            SyncWires();
            EnsureWiresOnTop();
        }
    }

    private void OnKey(object? s, KeyEventArgs e)
    {
        if (ViewModel is null)
            return;

        if (e.Key is Key.LeftCtrl or Key.RightCtrl)
        {
            _isWireInsertModifierPressed = true;
            if (_dragNode is not null)
                UpdateWireInsertPreview(_dragNode);
        }

        if (e.Key == Key.Space)
        {
            _isSpacePanArmed = true;
            if (!_isPanning)
                Cursor = new Cursor(StandardCursorType.Hand);
            e.Handled = true;
            return;
        }

        if (e.Key is Key.Left or Key.Right or Key.Up or Key.Down)
        {
            List<NodeViewModel> selected = ViewModel.Nodes.Where(n => n.IsSelected).ToList();
            if (selected.Count > 0)
            {
                double step = ViewModel.SnapToGrid ? CanvasViewModel.GridSize : 1.0;
                if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
                    step *= 10.0;

                double dx = 0, dy = 0;
                switch (e.Key)
                {
                    case Key.Left:
                        dx = -step;
                        break;
                    case Key.Right:
                        dx = step;
                        break;
                    case Key.Up:
                        dy = -step;
                        break;
                    case Key.Down:
                        dy = step;
                        break;
                }

                foreach (NodeViewModel n in selected)
                {
                    double nx = n.Position.X + dx;
                    double ny = n.Position.Y + dy;
                    if (ViewModel.SnapToGrid)
                    {
                        nx = CanvasViewModel.Snap(nx);
                        ny = CanvasViewModel.Snap(ny);
                    }
                    n.Position = new Point(nx, ny);
                }

                SyncWires();
                InvalidateArrange();
                e.Handled = true;
                return;
            }
        }

        if (e.Key == Key.F)
        {
            if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
            {
                if (FitSelectionInView())
                {
                    e.Handled = true;
                    return;
                }
            }

            if (CenterSelectionInView())
            {
                e.Handled = true;
                return;
            }
        }
        if (e.Key == Key.Escape)
        {
            _pinDrag?.CancelDrag();
            _wires.PendingConnection = null;
            _wires.InvalidateVisual();
            ClearHoverHighlights();
            ViewModel.DeselectAll();
            ViewModel.SearchMenu.Close();
            _isRubberBanding = false;
            UpdateRubberBandVisual();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.X && e.KeyModifiers.HasFlag(KeyModifiers.Control) && e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            if (TryBypassSelectedNode())
            {
                SyncWires();
                InvalidateArrange();
                e.Handled = true;
                return;
            }
        }

        if (e.Key == Key.Q && e.KeyModifiers.HasFlag(KeyModifiers.Alt))
        {
            if (SelectLinkedNodes(traverseUpstream: true))
            {
                SyncWires();
                InvalidateArrange();
                e.Handled = true;
                return;
            }
        }

        if (e.Key == Key.E && e.KeyModifiers.HasFlag(KeyModifiers.Alt))
        {
            if (SelectLinkedNodes(traverseUpstream: false))
            {
                SyncWires();
                InvalidateArrange();
                e.Handled = true;
                return;
            }
        }

        if (e.Key is Key.Delete or Key.Back)
        {
            if (TryDeleteWire(_hoveredWire))
            {
                e.Handled = true;
                return;
            }
        }
    }

    private void OnKeyUp(object? s, KeyEventArgs e)
    {
        if (e.Key is Key.LeftCtrl or Key.RightCtrl)
        {
            _isWireInsertModifierPressed = false;
            if (_dragNode is not null)
                UpdateWireInsertPreview(_dragNode);
        }

        if (e.Key != Key.Space)
            return;

        _isSpacePanArmed = false;
        if (!_isPanning)
            Cursor = Cursor.Default;
        e.Handled = true;
    }

    private void UpdateRubberBand()
    {
        if (ViewModel is null)
            return;
        var r = new Rect(
            Math.Min(_rubberStart.X, _rubberCurrent.X),
            Math.Min(_rubberStart.Y, _rubberCurrent.Y),
            Math.Abs(_rubberCurrent.X - _rubberStart.X),
            Math.Abs(_rubberCurrent.Y - _rubberStart.Y)
        );
        foreach (NodeViewModel n in ViewModel.Nodes)
            n.IsSelected = r.Contains(n.Position);
    }

    private void UpdateRubberBandVisual()
    {
        if (!_isRubberBanding)
        {
            if (_rubberBandRect != null)
            {
                _scene.Children.Remove(_rubberBandRect);
                _rubberBandRect = null;

                // Ensure wires stay on top after rubber band is removed
                EnsureWiresOnTop();
            }
            return;
        }

        double x = _panOffset.X + Math.Min(_rubberStart.X, _rubberCurrent.X) * _zoom;
        double y = _panOffset.Y + Math.Min(_rubberStart.Y, _rubberCurrent.Y) * _zoom;
        double w = Math.Abs(_rubberCurrent.X - _rubberStart.X) * _zoom;
        double h = Math.Abs(_rubberCurrent.Y - _rubberStart.Y) * _zoom;

        if (_rubberBandRect == null)
        {
            _rubberBandRect = new Border
            {
                Background = new SolidColorBrush(Color.Parse("#103B82F6")),
                BorderBrush = new SolidColorBrush(Color.Parse("#3B82F6")),
                BorderThickness = new Thickness(1),
                IsHitTestVisible = false,
            };
            // Add rubber band before wires so it renders behind
            int wiresIndex = _scene.Children.IndexOf(_wires);
            if (wiresIndex >= 0)
            {
                _scene.Children.Insert(wiresIndex, _rubberBandRect);
            }
            else
            {
                _scene.Children.Add(_rubberBandRect);
            }
        }

        Canvas.SetLeft(_rubberBandRect, x);
        Canvas.SetTop(_rubberBandRect, y);
        _rubberBandRect.Width = w;
        _rubberBandRect.Height = h;

        // Ensure wires remain visible during rubber band selection
        EnsureWiresOnTop();
        _wires.InvalidateVisual();
    }

    private void ShowContextMenu(Point screen)
    {
        if (ViewModel is null)
            return;

        Point canvasPoint = ScreenToCanvas(screen);
        ConnectionViewModel? wireUnderPointer = _wires.HitTestWire(canvasPoint, tolerance: 8);
        var sel = ViewModel.Nodes.Where(n => n.IsSelected).ToList();
        var menu = new ContextMenu();
        if (sel.Count > 0)
        {
            menu.Items.Add(
                new MenuItem
                {
                    Header = sel.Count == 1
                        ? string.Format(L("context.deleteSingle", "Delete {0}"), sel[0].Title)
                        : string.Format(L("context.deleteMultiple", "Delete {0} nodes"), sel.Count),
                    Command = new RelayCommand(ViewModel.DeleteSelected),
                }
            );

            if (sel.Count == 1 && sel[0].Type == NodeType.CteDefinition)
            {
                NodeViewModel cteNode = sel[0];
                menu.Items.Add(
                    new MenuItem
                    {
                        Header = LocalizationService.Instance["context.editCte"],
                        Command = new RelayCommand(() => _ = ViewModel.EnterCteEditorAsync(cteNode)),
                    }
                );
            }

            if (sel.Count == 1 && sel[0].Type == NodeType.ViewDefinition)
            {
                NodeViewModel viewNode = sel[0];
                menu.Items.Add(
                    new MenuItem
                    {
                        Header = LocalizationService.Instance["context.editViewSubcanvas"],
                        Command = new RelayCommand(() => _ = ViewModel.EnterViewEditorAsync(viewNode)),
                    }
                );
            }

            menu.Items.Add(
                new MenuItem
                {
                    Header = L("context.bringForward", "Bring Forward (Ctrl+PgUp)"),
                    Command = ViewModel.BringSelectionForwardCommand,
                }
            );
            menu.Items.Add(
                new MenuItem
                {
                    Header = L("context.sendBackward", "Send Backward (Ctrl+PgDown)"),
                    Command = ViewModel.SendSelectionBackwardCommand,
                }
            );
            menu.Items.Add(
                new MenuItem
                {
                    Header = L("context.bringToFront", "Bring to Front (Ctrl+Shift+PgUp)"),
                    Command = ViewModel.BringSelectionToFrontCommand,
                }
            );
            menu.Items.Add(
                new MenuItem
                {
                    Header = L("context.sendToBack", "Send to Back (Ctrl+Shift+PgDown)"),
                    Command = ViewModel.SendSelectionToBackCommand,
                }
            );
            menu.Items.Add(
                new MenuItem
                {
                    Header = L("context.normalizeLayers", "Normalize Layers"),
                    Command = ViewModel.NormalizeLayersCommand,
                }
            );

            menu.Items.Add(new Separator());
            menu.Items.Add(
                new MenuItem
                {
                    Header = "Node Wrangler: Bypass selected (Ctrl+Shift+X)",
                    Command = new RelayCommand(() =>
                    {
                        if (TryBypassSelectedNode())
                        {
                            SyncWires();
                            InvalidateArrange();
                        }
                    }),
                }
            );
            menu.Items.Add(
                new MenuItem
                {
                    Header = "Node Wrangler: Select upstream (Alt+Q)",
                    Command = new RelayCommand(() =>
                    {
                        if (SelectLinkedNodes(traverseUpstream: true))
                        {
                            SyncWires();
                            InvalidateArrange();
                        }
                    }),
                }
            );
            menu.Items.Add(
                new MenuItem
                {
                    Header = "Node Wrangler: Select downstream (Alt+E)",
                    Command = new RelayCommand(() =>
                    {
                        if (SelectLinkedNodes(traverseUpstream: false))
                        {
                            SyncWires();
                            InvalidateArrange();
                        }
                    }),
                }
            );
        }
        else if (wireUnderPointer is not null)
        {
            menu.Items.Add(
                new MenuItem
                {
                    Header = L("context.deleteWire", "Delete wire"),
                    Command = new RelayCommand(() => TryDeleteWire(wireUnderPointer)),
                }
            );
        }
        else
        {
            menu.Items.Add(
                new MenuItem
                {
                    Header = L("context.addNode", "Add Node (Shift+A)"),
                    Command = new RelayCommand(() =>
                        ViewModel.SearchMenu.Open(ScreenToCanvas(screen))
                    ),
                }
            );
        }

        menu.Items.Add(new Separator());
        menu.Items.Add(
            new MenuItem
            {
                Header = string.Format(L("context.undoWithDescription", "Undo {0}"), ViewModel.UndoRedo.UndoDescription),
                Command = ViewModel.UndoCommand,
            }
        );
        menu.Items.Add(new MenuItem { Header = L("context.redo", "Redo"), Command = ViewModel.RedoCommand });
        menu.Open(this);
    }

    private static string L(string key, string fallback)
    {
        string value = LocalizationService.Instance[key];
        return string.Equals(value, key, StringComparison.Ordinal) ? fallback : value;
    }

    private bool IsWireInsertModifierActive()
    {
        return _isWireInsertModifierPressed;
    }

    private void SetWireInsertPreviewPins(PinViewModel? inputPin, PinViewModel? outputPin)
    {
        if (_wireInsertPreviewInputPin is not null)
            _wireInsertPreviewInputPin.IsDropTarget = false;
        if (_wireInsertPreviewOutputPin is not null)
            _wireInsertPreviewOutputPin.IsDropTarget = false;

        _wireInsertPreviewInputPin = inputPin;
        _wireInsertPreviewOutputPin = outputPin;

        if (_wireInsertPreviewInputPin is not null)
            _wireInsertPreviewInputPin.IsDropTarget = true;
        if (_wireInsertPreviewOutputPin is not null)
            _wireInsertPreviewOutputPin.IsDropTarget = true;
    }

    private void SetWireInsertPreviewInvalidWire(ConnectionViewModel? wire)
    {
        _wireInsertPreviewInvalidWire = wire;
        _wires.InvalidPreviewConnection = wire;
    }

    private void UpdateWireInsertPreview(NodeViewModel node)
    {
        if (ViewModel is null)
            return;

        CanvasHoverHighlighter.ClearHover(ViewModel);
        SetWireInsertPreviewPins(null, null);
        SetWireInsertPreviewInvalidWire(null);

        if (!IsWireInsertModifierActive())
        {
            _hoveredPin = null;
            _hoveredWire = null;
            _wires.InvalidateVisual();
            return;
        }

        ConnectionViewModel? wire = FindWireUnderNode(node);
        if (wire?.ToPin is null)
        {
            _hoveredPin = null;
            _hoveredWire = null;
            _wires.InvalidateVisual();
            return;
        }

        PinViewModel? insertInput = node.InputPins.FirstOrDefault(p => p.CanAccept(wire.FromPin));
        PinViewModel? insertOutput = node.OutputPins.FirstOrDefault(p => wire.ToPin.CanAccept(p));

        if (insertInput is null || insertOutput is null)
        {
            wire.IsHighlighted = true;
            wire.FromPin.IsHovered = true;
            wire.ToPin.IsHovered = true;
            SetWireInsertPreviewInvalidWire(wire);
            _hoveredPin = null;
            _hoveredWire = wire;
            _wires.InvalidateVisual();
            return;
        }

        wire.IsHighlighted = true;
        wire.FromPin.IsHovered = true;
        wire.ToPin.IsHovered = true;
        insertInput.IsHovered = true;
        insertOutput.IsHovered = true;
        SetWireInsertPreviewPins(insertInput, insertOutput);
        SetWireInsertPreviewInvalidWire(null);

        _hoveredPin = insertInput;
        _hoveredWire = wire;
        _wires.InvalidateVisual();
    }

    private bool TryInsertNodeOnWire(NodeViewModel node)
    {
        if (ViewModel is null || !IsWireInsertModifierActive())
            return false;

        ConnectionViewModel? wire = FindWireUnderNode(node);
        if (wire?.ToPin is null)
            return false;

        if (ReferenceEquals(wire.FromPin.Owner, node) || ReferenceEquals(wire.ToPin.Owner, node))
            return false;

        PinViewModel? insertInput = node.InputPins.FirstOrDefault(p => p.CanAccept(wire.FromPin));
        if (insertInput is null)
            return false;

        PinViewModel? insertOutput = node.OutputPins.FirstOrDefault(p => wire.ToPin.CanAccept(p));
        if (insertOutput is null)
            return false;

        using UndoRedoStack.UndoRedoTransaction tx = ViewModel.UndoRedo.BeginTransaction("Insert node on wire");

        ViewModel.DeleteConnection(wire);
        ViewModel.ConnectPins(wire.FromPin, insertInput);
        ViewModel.ConnectPins(insertOutput, wire.ToPin);

        bool insertedIn = ViewModel.Connections.Any(c =>
            ReferenceEquals(c.FromPin, wire.FromPin) && ReferenceEquals(c.ToPin, insertInput));
        bool insertedOut = ViewModel.Connections.Any(c =>
            ReferenceEquals(c.FromPin, insertOutput) && ReferenceEquals(c.ToPin, wire.ToPin));

        if (!insertedIn || !insertedOut)
            return false;

        tx.Commit();
        return true;
    }

    private ConnectionViewModel? FindWireUnderNode(NodeViewModel node)
    {
        if (!_nodeControlCache.TryGetValue(node, out NodeControl? control))
            return null;

        Point center = new(
            node.Position.X + Math.Max(control.Bounds.Width, 1) / 2,
            node.Position.Y + Math.Max(control.Bounds.Height, 1) / 2
        );

        return _wires.HitTestWire(center, tolerance: 16);
    }

    private bool TryBypassSelectedNode()
    {
        if (ViewModel is null)
            return false;

        NodeViewModel? selected = ViewModel.Nodes.FirstOrDefault(n => n.IsSelected);
        if (selected is null)
            return false;

        List<ConnectionViewModel> incoming =
        [
            .. ViewModel.Connections.Where(c => c.ToPin is not null && ReferenceEquals(c.ToPin.Owner, selected)),
        ];
        List<ConnectionViewModel> outgoing =
        [
            .. ViewModel.Connections.Where(c => c.ToPin is not null && ReferenceEquals(c.FromPin.Owner, selected)),
        ];

        ConnectionViewModel? inConn = null;
        ConnectionViewModel? outConn = null;

        foreach (ConnectionViewModel candidateIn in incoming)
        {
            ConnectionViewModel? candidateOut = outgoing.FirstOrDefault(o => o.ToPin is not null && o.ToPin.CanAccept(candidateIn.FromPin));
            if (candidateOut is null)
                continue;

            inConn = candidateIn;
            outConn = candidateOut;
            break;
        }

        if (inConn is null || outConn?.ToPin is null)
            return false;

        using UndoRedoStack.UndoRedoTransaction tx = ViewModel.UndoRedo.BeginTransaction("Bypass selected node");
        ViewModel.ConnectPins(inConn.FromPin, outConn.ToPin);
        ViewModel.DeleteSelected();
        tx.Commit();
        return true;
    }

    private bool SelectLinkedNodes(bool traverseUpstream)
    {
        if (ViewModel is null)
            return false;

        List<NodeViewModel> selected = [.. ViewModel.Nodes.Where(n => n.IsSelected)];
        if (selected.Count == 0)
            return false;

        var visited = new HashSet<NodeViewModel>(selected);
        var queue = new Queue<NodeViewModel>(selected);

        while (queue.Count > 0)
        {
            NodeViewModel current = queue.Dequeue();

            IEnumerable<NodeViewModel> linked = traverseUpstream
                ? ViewModel.Connections
                    .Where(c => c.ToPin is not null && ReferenceEquals(c.ToPin.Owner, current))
                    .Select(c => c.FromPin.Owner)
                : ViewModel.Connections
                    .Where(c => c.ToPin is not null && ReferenceEquals(c.FromPin.Owner, current))
                    .Select(c => c.ToPin!.Owner);

            foreach (NodeViewModel neighbor in linked)
            {
                if (!visited.Add(neighbor))
                    continue;

                queue.Enqueue(neighbor);
            }
        }

        ViewModel.DeselectAll();
        foreach (NodeViewModel node in visited)
            node.IsSelected = true;

        return true;
    }

    /// <summary>
    /// Detects horizontal and vertical alignment between the dragged node and all
    /// other (non-selected) nodes. Guides appear for: left edge, right edge,
    /// vertical centre, top edge, bottom edge, horizontal centre.
    /// </summary>
    private void UpdateAlignGuides(NodeViewModel dragged)
    {
        if (ViewModel is null)
        {
            _guides.ClearGuides();
            return;
        }

        var others = ViewModel.Nodes.Where(n => n != dragged && !n.IsSelected).ToList();

        double dX = dragged.Position.X;
        double dW = dragged.Width > 0 ? dragged.Width : 230;
        double dCX = dX + dW / 2.0;
        double dRX = dX + dW;

        double dY = dragged.Position.Y;
        double dH = DefaultNodeH;
        double dCY = dY + dH / 2.0;
        double dBY = dY + dH;

        var hGuides = new List<double>(); // Y coordinates of horizontal guides
        var vGuides = new List<double>(); // X coordinates of vertical guides

        foreach (NodeViewModel n in others)
        {
            double oX = n.Position.X;
            double oW = n.Width > 0 ? n.Width : 230;
            double oCX = oX + oW / 2.0;
            double oRX = oX + oW;

            double oY = n.Position.Y;
            double oH = DefaultNodeH;
            double oCY = oY + oH / 2.0;
            double oBY = oY + oH;

            // Vertical guides (X alignment)
            if (Math.Abs(dX - oX) < GuideThreshold)
                vGuides.Add(oX);
            if (Math.Abs(dX - oRX) < GuideThreshold)
                vGuides.Add(oRX);
            if (Math.Abs(dCX - oCX) < GuideThreshold)
                vGuides.Add(oCX);
            if (Math.Abs(dRX - oX) < GuideThreshold)
                vGuides.Add(oX);
            if (Math.Abs(dRX - oRX) < GuideThreshold)
                vGuides.Add(oRX);

            // Horizontal guides (Y alignment)
            if (Math.Abs(dY - oY) < GuideThreshold)
                hGuides.Add(oY);
            if (Math.Abs(dY - oBY) < GuideThreshold)
                hGuides.Add(oBY);
            if (Math.Abs(dCY - oCY) < GuideThreshold)
                hGuides.Add(oCY);
            if (Math.Abs(dBY - oY) < GuideThreshold)
                hGuides.Add(oY);
            if (Math.Abs(dBY - oBY) < GuideThreshold)
                hGuides.Add(oBY);
        }

        _guides.SetGuides([.. hGuides.Distinct()], [.. vGuides.Distinct()]);
        if (hGuides.Count > 0 || vGuides.Count > 0)
            Log($"    Guides updated: {hGuides.Distinct().Count()} h, {vGuides.Distinct().Count()} v");
    }
}
