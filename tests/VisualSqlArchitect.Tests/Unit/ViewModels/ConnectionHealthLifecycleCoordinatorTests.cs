using VisualSqlArchitect.UI.Services.ConnectionManager;
using VisualSqlArchitect.UI.Services.Benchmark;
using VisualSqlArchitect.Core;
using VisualSqlArchitect.UI.ViewModels;

namespace VisualSqlArchitect.Tests.Unit.ViewModels;

public class ConnectionHealthLifecycleCoordinatorTests
{
    [Fact]
    public async Task EvaluateActiveStatusAsync_UsesMatchingProfileById()
    {
        var monitor = new FakeConnectionHealthMonitorService
        {
            NextStatus = EConnectionHealthStatus.Degraded,
        };
        var coordinator = new ConnectionHealthLifecycleCoordinator(monitor);

        var profiles = new List<ConnectionProfile>
        {
            new()
            {
                Id = "p-1",
                Name = "A",
                Provider = DatabaseProvider.Postgres,
                Host = "localhost",
                Port = 5432,
                Database = "db1",
                Username = "u",
                Password = "p",
                TimeoutSeconds = 30,
            },
            new()
            {
                Id = "p-2",
                Name = "B",
                Provider = DatabaseProvider.SQLite,
                Host = "localhost",
                Port = 0,
                Database = "db2",
                Username = "u",
                Password = "p",
                TimeoutSeconds = 10,
            },
        };

        EConnectionHealthStatus status = await coordinator.EvaluateActiveStatusAsync(
            profiles,
            activeProfileId: "p-2",
            runTestAsync: (_, _, _, _) => Task.FromResult(new ConnectionTestResult(true, null, null)),
            degradedLatencyThresholdMs: 500);

        Assert.Equal(EConnectionHealthStatus.Degraded, status);
        Assert.Equal("p-2", monitor.LastProfileId);
    }

    [Fact]
    public void Restart_ForwardsToHealthMonitorService()
    {
        var monitor = new FakeConnectionHealthMonitorService();
        var coordinator = new ConnectionHealthLifecycleCoordinator(monitor);

        bool started = false;
        CancellationTokenSource? cts = coordinator.Restart("active", existing: null, _ => started = true);

        Assert.NotNull(cts);
        Assert.True(started);
        Assert.Equal("active", monitor.LastRestartProfileId);
        cts!.Dispose();
    }

    private sealed class FakeConnectionHealthMonitorService : IConnectionHealthMonitorService
    {
        public EConnectionHealthStatus NextStatus { get; set; } = EConnectionHealthStatus.Unknown;
        public string? LastProfileId { get; private set; }
        public string? LastRestartProfileId { get; private set; }

        public CancellationTokenSource? Restart(
            string? activeProfileId,
            CancellationTokenSource? existing,
            Action<CancellationToken> startLoop)
        {
            LastRestartProfileId = activeProfileId;
            existing?.Cancel();
            existing?.Dispose();

            if (activeProfileId is null)
                return null;

            var cts = new CancellationTokenSource();
            startLoop(cts.Token);
            return cts;
        }

        public Task HealthMonitorLoopAsync(CancellationToken ct, Func<CancellationToken, Task> runHealthCheckAsync)
            => Task.CompletedTask;

        public Task<EConnectionHealthStatus> EvaluateStatusAsync(
            ConnectionProfile? profile,
            Func<ConnectionConfig, DatabaseProvider, int, CancellationToken, Task<ConnectionTestResult>> runTestAsync,
            double degradedLatencyThresholdMs,
            CancellationToken ct = default)
        {
            LastProfileId = profile?.Id;
            return Task.FromResult(NextStatus);
        }
    }
}


