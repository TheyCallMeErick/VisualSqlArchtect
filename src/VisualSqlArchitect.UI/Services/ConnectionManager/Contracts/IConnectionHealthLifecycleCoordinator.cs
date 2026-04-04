using VisualSqlArchitect.Core;

namespace VisualSqlArchitect.UI.Services.ConnectionManager;

public interface IConnectionHealthLifecycleCoordinator
{
    CancellationTokenSource? Restart(
        string? activeProfileId,
        CancellationTokenSource? existing,
        Action<CancellationToken> startLoop);

    Task<EConnectionHealthStatus> EvaluateActiveStatusAsync(
        IReadOnlyCollection<ConnectionProfile> profiles,
        string? activeProfileId,
        Func<ConnectionConfig, DatabaseProvider, int, CancellationToken, Task<ConnectionTestResult>> runTestAsync,
        double degradedLatencyThresholdMs,
        CancellationToken ct = default);
}

