using DBWeaver.Core;
using DBWeaver.UI.Services.Explain;
using DBWeaver.UI.Services.SqlEditor;
using DBWeaver.UI.ViewModels.Canvas;

namespace DBWeaver.Tests.Unit.Services.SqlEditor;

public sealed class SqlEditorExecutionControllerTests
{
    [Fact]
    public async Task RunExplainAsync_DelegatesToExplainServiceWithExpectedOptions()
    {
        var executor = new TrackingExplainExecutor();
        var explainService = new SqlEditorExplainService(executor);
        var sut = new SqlEditorExecutionController(explainService: explainService);

        ExplainResult result = await sut.RunExplainAsync(
            statement: "SELECT 1",
            provider: DatabaseProvider.Postgres,
            connectionConfig: null,
            includeAnalyze: true,
            cancellationToken: CancellationToken.None);

        Assert.Same(executor.ResultToReturn, result);
        Assert.Equal("SELECT 1", executor.CapturedSql);
        Assert.Equal(DatabaseProvider.Postgres, executor.CapturedProvider);
        Assert.NotNull(executor.CapturedOptions);
        Assert.True(executor.CapturedOptions!.IncludeAnalyze);
        Assert.False(executor.CapturedOptions.IncludeBuffers);
        Assert.Equal(ExplainFormat.Text, executor.CapturedOptions.Format);
    }

    private sealed class TrackingExplainExecutor : IExplainExecutor
    {
        public string? CapturedSql { get; private set; }
        public DatabaseProvider CapturedProvider { get; private set; }
        public ExplainOptions? CapturedOptions { get; private set; }

        public ExplainResult ResultToReturn { get; } = new(
            Nodes: [],
            PlanningTimeMs: 1,
            ExecutionTimeMs: 2,
            RawOutput: "ok",
            IsSimulated: true);

        public Task<ExplainResult> RunAsync(
            string sql,
            DatabaseProvider provider,
            ConnectionConfig? connectionConfig,
            ExplainOptions options,
            CancellationToken ct = default)
        {
            CapturedSql = sql;
            CapturedProvider = provider;
            CapturedOptions = options;
            return Task.FromResult(ResultToReturn);
        }
    }
}
