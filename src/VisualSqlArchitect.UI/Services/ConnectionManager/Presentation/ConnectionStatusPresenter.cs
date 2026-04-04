using VisualSqlArchitect.UI.Services.Localization;

namespace VisualSqlArchitect.UI.Services.ConnectionManager;

public sealed class ConnectionStatusPresenter(ILocalizationService localization) : IConnectionStatusPresenter
{
    private readonly ILocalizationService _localization = localization;

    public ConnectionStatusViewState Connecting() =>
        new(_localization["connection.status.connecting"], "#FBBF24");

    public ConnectionStatusViewState Testing() =>
        new(_localization["connection.status.testing"], "#FBBF24");

    public ConnectionStatusViewState Connected() =>
        new(_localization["connection.status.connected"], "#4ADE80");

    public ConnectionStatusViewState TestSuccess(TimeSpan? latency, double degradedLatencyThresholdMs)
    {
        double ms = latency?.TotalMilliseconds ?? 0;
        bool degraded = ms >= degradedLatencyThresholdMs;
        string lag = degraded
            ? $" -- {L("connection.status.highLatency", "High latency")} ({ms:0}ms)"
            : $" Â· {ms:0}ms";
        string message = $"{_localization["connection.status.connected"]}{lag}";
        return new ConnectionStatusViewState(message, degraded ? "#FBBF24" : "#4ADE80");
    }

    public ConnectionStatusViewState MetadataUnavailable() =>
        new(_localization["connection.status.metadataUnavailable"], "#EF4444");

    public ConnectionStatusViewState Cancelled() =>
        new(_localization["connection.status.cancelled"], "#9CA3AF");

    public ConnectionStatusViewState Failed(string message) =>
        new(message, "#EF4444");

    public ConnectionStatusViewState FailedWithPrefix(string reason) =>
        new($"{_localization["connection.status.failedPrefix"]}: {reason}", "#EF4444");

    private string L(string key, string fallback)
    {
        string value = _localization[key];
        return string.Equals(value, key, StringComparison.Ordinal) ? fallback : value;
    }
}

