using VisualSqlArchitect.Core;

namespace VisualSqlArchitect.UI.Services.ConnectionManager;

public interface IConnectionHealthMonitorService
{
    CancellationTokenSource? Restart(
        string? activeProfileId,
        CancellationTokenSource? existing,
        Action<CancellationToken> startLoop);

    Task HealthMonitorLoopAsync(CancellationToken ct, Func<CancellationToken, Task> runHealthCheckAsync);

    Task<EConnectionHealthStatus> EvaluateStatusAsync(
        ConnectionProfile? profile,
        Func<ConnectionConfig, DatabaseProvider, int, CancellationToken, Task<ConnectionTestResult>> runTestAsync,
        double degradedLatencyThresholdMs,
        CancellationToken ct = default);
}

