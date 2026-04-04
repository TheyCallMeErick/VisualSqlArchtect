using System.Globalization;
using VisualSqlArchitect.UI.ViewModels.Canvas;

namespace VisualSqlArchitect.UI.Services.Explain;

public sealed record ExplainHistoryState(
    DateTimeOffset TimestampUtc,
    string TopOperation,
    double? TopCost,
    int AlertCount
);

public sealed class ExplainHistoryItem
{
    public DateTimeOffset TimestampUtc { get; init; }
    public string TopOperation { get; init; } = string.Empty;
    public double? TopCost { get; init; }
    public int AlertCount { get; init; }

    public string TimeText => TimestampUtc.ToLocalTime().ToString("HH:mm");
    public string CostText => TopCost.HasValue ? TopCost.Value.ToString("0.##", CultureInfo.InvariantCulture) : "-";
    public string AlertText => $"{AlertCount} alertas";

    public ExplainHistoryState ToState() =>
        new(TimestampUtc, TopOperation, TopCost, AlertCount);

    public static ExplainHistoryItem FromState(ExplainHistoryState state) =>
        new()
        {
            TimestampUtc = state.TimestampUtc,
            TopOperation = state.TopOperation,
            TopCost = state.TopCost,
            AlertCount = state.AlertCount,
        };
}



