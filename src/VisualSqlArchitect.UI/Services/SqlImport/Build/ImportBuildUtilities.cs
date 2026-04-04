using VisualSqlArchitect.UI.ViewModels;
using VisualSqlArchitect.UI.ViewModels.Canvas;

namespace VisualSqlArchitect.UI.Services.SqlImport.Build;

internal static class ImportBuildUtilities
{
    public static void SafeWire(NodeViewModel from, string fromPin, NodeViewModel to, string toPin, CanvasViewModel canvas)
    {
        PinViewModel? fromResolvedPin =
            from.OutputPins.FirstOrDefault(p => p.Name.Equals(fromPin, StringComparison.OrdinalIgnoreCase))
            ?? from.InputPins.FirstOrDefault(p => p.Name.Equals(fromPin, StringComparison.OrdinalIgnoreCase));

        PinViewModel? toResolvedPin =
            to.InputPins.FirstOrDefault(p => p.Name.Equals(toPin, StringComparison.OrdinalIgnoreCase))
            ?? to.OutputPins.FirstOrDefault(p => p.Name.Equals(toPin, StringComparison.OrdinalIgnoreCase));

        if (fromResolvedPin is null || toResolvedPin is null)
            return;

        if (!toResolvedPin.CanAccept(fromResolvedPin))
            return;

        var conn = new ConnectionViewModel(fromResolvedPin, default, default) { ToPin = toResolvedPin };
        fromResolvedPin.IsConnected = true;
        toResolvedPin.IsConnected = true;
        canvas.Connections.Add(conn);
    }

    public static string NormalizeJoinType(string rawJoinType)
    {
        string normalized = rawJoinType.ToUpperInvariant();
        if (normalized.Contains("LEFT", StringComparison.Ordinal))
            return "LEFT";
        if (normalized.Contains("RIGHT", StringComparison.Ordinal))
            return "RIGHT";
        if (normalized.Contains("FULL", StringComparison.Ordinal))
            return "FULL";
        if (normalized.Contains("CROSS", StringComparison.Ordinal))
            return "CROSS";
        return "INNER";
    }
}
