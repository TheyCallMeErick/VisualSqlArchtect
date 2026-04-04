using Avalonia;
using Avalonia.Media;
using VisualSqlArchitect.CanvasKit;
using VisualSqlArchitect.Nodes;
using VisualSqlArchitect.Nodes.PinTypes;

namespace VisualSqlArchitect.UI.ViewModels;

/// <summary>
/// Represents a connection (wire) entre two pins in the canvas.
/// Manages the visual and data flow representation of the connection.
/// </summary>
public sealed class ConnectionViewModel : ViewModelBase
{
    // Backward compatibility for existing tests and call sites.
    public enum EWireDashKind
    {
        Solid,
        ShortDash,
        MediumDash,
        LongDash,
        WideDash,
        Dotted,
    }

    private Point _fromPoint;
    private Point _toPoint;
    private bool _isHighlighted;

    /// <summary>Unique identifier for this connection.</summary>
    public string Id { get; } = Guid.NewGuid().ToString("N")[..8];

    /// <summary>The pin data flows FROM (must be Output).</summary>
    public PinViewModel FromPin { get; }

    public ConnectionViewModel(PinViewModel fromPin, Point fromPoint, Point toPoint)
    {
        FromPin = fromPin;
        _fromPoint = fromPoint;
        _toPoint = toPoint;
    }

    /// <summary>The pin data flows TO (must be Input). Can be null while dragging.</summary>
    public PinViewModel? ToPin { get; set; }

    /// <summary>Screen position of the source pin.</summary>
    public Point FromPoint
    {
        get => _fromPoint;
        set
        {
            Set(ref _fromPoint, value);
            RaisePropertyChanged(nameof(BezierPath));
        }
    }

    /// <summary>Screen position of the target pin.</summary>
    public Point ToPoint
    {
        get => _toPoint;
        set
        {
            Set(ref _toPoint, value);
            RaisePropertyChanged(nameof(BezierPath));
        }
    }

    /// <summary>True if this connection is currently highlighted (hovered).</summary>
    public bool IsHighlighted
    {
        get => _isHighlighted;
        set => Set(ref _isHighlighted, value);
    }

    /// <summary>Wire color matches the source pin's data type color.</summary>
    public Color WireColor => FromPin.PinColor;

    /// <summary>Opacity increases when highlighted.</summary>
    public double WireOpacity => IsHighlighted ? 1.0 : 0.75;

    public CanvasWireDashKind CanvasDashKind => PinTypeRegistry.GetType(FromPin.EffectiveDataType).WireDashKind switch
    {
        PinWireDashKind.ShortDash => CanvasWireDashKind.ShortDash,
        PinWireDashKind.MediumDash => CanvasWireDashKind.MediumDash,
        PinWireDashKind.LongDash => CanvasWireDashKind.LongDash,
        PinWireDashKind.WideDash => CanvasWireDashKind.WideDash,
        PinWireDashKind.Dotted => CanvasWireDashKind.Dotted,
        _ => CanvasWireDashKind.Solid,
    };

    public EWireDashKind DashKind => CanvasDashKind switch
    {
        CanvasWireDashKind.ShortDash => EWireDashKind.ShortDash,
        CanvasWireDashKind.MediumDash => EWireDashKind.MediumDash,
        CanvasWireDashKind.LongDash => EWireDashKind.LongDash,
        CanvasWireDashKind.WideDash => EWireDashKind.WideDash,
        CanvasWireDashKind.Dotted => EWireDashKind.Dotted,
        _ => EWireDashKind.Solid,
    };

    /// <summary>Thickness varies by pin family and increases when highlighted.</summary>
    public double WireThickness
    {
        get
        {
            double baseThickness = PinTypeRegistry.GetType(FromPin.EffectiveDataType).WireThickness;
            return CanvasWireStylePolicy.ResolveThickness(baseThickness, IsHighlighted);
        }
    }

    /// <summary>
    /// Generates a smooth cubic Bezier curve from FromPoint to ToPoint.
    /// Control points create smooth, readable connections.
    /// </summary>
    public string BezierPath
    {
        get => CanvasWireGeometry.BuildBezierPath(FromPoint.X, FromPoint.Y, ToPoint.X, ToPoint.Y);
    }
}
