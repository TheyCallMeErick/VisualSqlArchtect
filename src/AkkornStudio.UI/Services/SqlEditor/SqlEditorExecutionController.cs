using AkkornStudio.Core;
using AkkornStudio.UI.Services.Benchmark;
using AkkornStudio.UI.Services.ConnectionManager.Models;
using AkkornStudio.UI.Services.Explain;

namespace AkkornStudio.UI.Services.SqlEditor;

public sealed class SqlEditorExecutionController
{
    private readonly SqlEditorExplainService _explainService;
    private readonly SqlEditorBenchmarkService _benchmarkService;

    public SqlEditorExecutionController(
        SqlEditorExplainService? explainService = null,
        SqlEditorBenchmarkService? benchmarkService = null)
    {
        _explainService = explainService ?? new SqlEditorExplainService();
        _benchmarkService = benchmarkService ?? new SqlEditorBenchmarkService();
    }

    public Task<ExplainResult> RunExplainAsync(
        string statement,
        DatabaseProvider provider,
        ConnectionConfig? connectionConfig,
        bool includeAnalyze,
        CancellationToken cancellationToken)
    {
        return _explainService.RunAsync(
            statement,
            provider,
            connectionConfig,
            includeAnalyze,
            cancellationToken);
    }

    public Task<BenchmarkRunResult> RunBenchmarkAsync(
        string statement,
        Func<ConnectionConfig?> connectionConfigResolver,
        int iterations,
        int warmupIterations,
        int intervalMs,
        Action<BenchmarkRunProgress> onProgress,
        CancellationToken cancellationToken)
    {
        return _benchmarkService.ExecuteAsync(
            statement,
            connectionConfigResolver,
            iterations,
            warmupIterations,
            intervalMs,
            onProgress,
            cancellationToken);
    }
}
