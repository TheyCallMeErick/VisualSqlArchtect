using AkkornStudio.UI.Services.Benchmark;
using AkkornStudio.UI.Services.ConnectionManager.Models;

namespace AkkornStudio.UI.Services.SqlEditor;

public sealed class SqlEditorBenchmarkService
{
    public Task<BenchmarkRunResult> ExecuteAsync(
        string sql,
        Func<ConnectionConfig?> connectionResolver,
        int iterations,
        int warmupIterations,
        int intervalMs,
        Action<BenchmarkRunProgress>? onProgress,
        CancellationToken cancellationToken)
    {
        var iterationExecutor = new AdaptiveBenchmarkIterationExecutor(
            connectionResolver: connectionResolver,
            sqlResolver: () => sql);
        var runner = new BenchmarkRunner(iterationExecutor);
        var executionService = new BenchmarkExecutionService(runner);

        BenchmarkRunConfiguration config = new(iterations, warmupIterations, intervalMs);
        return executionService.ExecuteAsync(
            runLabel: $"SQL Editor {DateTime.Now:HH:mm:ss}",
            configuration: config,
            onProgress: onProgress,
            cancellationToken: cancellationToken);
    }
}
