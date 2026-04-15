using DBWeaver.Core;
using DBWeaver.UI.Services.SqlEditor;

namespace DBWeaver.Tests.Unit.Services.SqlEditor;

public sealed class SqlCompletionWorkerTests
{
    [Fact]
    public async Task RequestAsync_WhenNewRequestArrives_CancelsObsoleteAndReturnsLatest()
    {
        var engine = new ControllableCompletionEngine();
        await using var sut = new SqlCompletionWorker(engine);

        var firstRequest = new SqlCompletionRequestContext(
            FullText: "SELECT * FROM first",
            CaretOffset: 19,
            Metadata: null,
            Provider: DatabaseProvider.Postgres,
            ConnectionProfileId: "conn-a");

        Task<SqlCompletionStageSnapshot> firstTask = sut.RequestAsync(firstRequest);
        await engine.FirstRequestStarted.Task;

        var secondRequest = new SqlCompletionRequestContext(
            FullText: "SELECT * FROM second",
            CaretOffset: 20,
            Metadata: null,
            Provider: DatabaseProvider.Postgres,
            ConnectionProfileId: "conn-a");

        SqlCompletionStageSnapshot secondResult = await sut.RequestAsync(secondRequest);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await firstTask);
        Assert.Equal(SqlCompletionPipelineStage.Final, secondResult.Stage);
        Assert.Equal("LATEST", Assert.Single(secondResult.Request.Suggestions).Label);
        Assert.True(secondResult.Telemetry.CancelledRequests >= 1);
        Assert.True(secondResult.Telemetry.WorkerExecutionMs >= 0);
        Assert.True(secondResult.Telemetry.WorkerDispatchDelayMs >= 0);
    }

    [Fact]
    public async Task RequestAsync_ReportsProgressiveStages()
    {
        var engine = new ControllableCompletionEngine();
        await using var sut = new SqlCompletionWorker(engine);
        var stages = new List<SqlCompletionPipelineStage>();
        var progress = new InlineProgress<SqlCompletionStageSnapshot>(snapshot => stages.Add(snapshot.Stage));

        var request = new SqlCompletionRequestContext(
            FullText: "SEL",
            CaretOffset: 3,
            Metadata: null,
            Provider: DatabaseProvider.Postgres,
            ConnectionProfileId: "conn-a");

        SqlCompletionStageSnapshot result = await sut.RequestAsync(request, progress);

        Assert.Equal(SqlCompletionPipelineStage.Final, result.Stage);
        Assert.Equal(2, stages.Count);
        Assert.Equal(SqlCompletionPipelineStage.Tier0, stages[0]);
        Assert.Equal(SqlCompletionPipelineStage.Tier3, stages[1]);
    }

    private sealed class ControllableCompletionEngine : ISqlCompletionEngine
    {
        public TaskCompletionSource<bool> FirstRequestStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public SqlCompletionStageSnapshot BuildCompletion(
            SqlCompletionRequestContext request,
            IProgress<SqlCompletionStageSnapshot>? progress = null,
            CancellationToken cancellationToken = default)
        {
            if (request.FullText.Contains("first", StringComparison.OrdinalIgnoreCase))
            {
                FirstRequestStarted.TrySetResult(true);
                WaitForCancellation(cancellationToken);
            }

            var tier0Request = new SqlCompletionRequest
            {
                PrefixLength = 0,
                Suggestions = [new SqlCompletionSuggestion("SELECT", "SELECT", "keyword", SqlCompletionKind.Keyword)],
            };

            progress?.Report(new SqlCompletionStageSnapshot(
                SqlCompletionPipelineStage.Tier0,
                tier0Request,
                new SqlCompletionTelemetry { CancelledRequests = request.CancelledRequests },
                IsFinal: false));

            var tier3Request = new SqlCompletionRequest
            {
                PrefixLength = 0,
                Suggestions = [new SqlCompletionSuggestion("LATEST", "LATEST", "latest", SqlCompletionKind.Keyword)],
            };

            progress?.Report(new SqlCompletionStageSnapshot(
                SqlCompletionPipelineStage.Tier3,
                tier3Request,
                new SqlCompletionTelemetry { CancelledRequests = request.CancelledRequests },
                IsFinal: false));

            return new SqlCompletionStageSnapshot(
                SqlCompletionPipelineStage.Final,
                tier3Request,
                new SqlCompletionTelemetry { CancelledRequests = request.CancelledRequests },
                IsFinal: true);
        }

        public SqlCompletionRequest GetSuggestions(
            string fullText,
            int caretOffset,
            DBWeaver.Metadata.DbMetadata? metadata,
            DatabaseProvider? provider = null,
            string? connectionProfileId = null,
            CancellationToken cancellationToken = default)
            => new();

        private static void WaitForCancellation(CancellationToken cancellationToken)
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Thread.Sleep(5);
            }
        }
    }

    private sealed class InlineProgress<T> : IProgress<T>
    {
        private readonly Action<T> _report;

        public InlineProgress(Action<T> report)
        {
            _report = report;
        }

        public void Report(T value)
        {
            _report(value);
        }
    }
}
