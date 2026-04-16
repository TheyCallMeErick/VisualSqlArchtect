using System.Diagnostics;

namespace AkkornStudio.UI.Services.SqlEditor;

public sealed class SqlCompletionWorker : ISqlCompletionWorker
{
    private readonly ISqlCompletionEngine _engine;
    private readonly object _gate = new();
    private CancellationTokenSource? _currentJobCts;
    private bool _isDisposed;
    private int _cancelledRequests;

    public SqlCompletionWorker(ISqlCompletionEngine engine)
    {
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
    }

    public Task<SqlCompletionStageSnapshot> RequestAsync(
        SqlCompletionRequestContext request,
        IProgress<SqlCompletionStageSnapshot>? progress = null,
        CancellationToken cancellationToken = default)
    {
        CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        long enqueueTimestamp = Stopwatch.GetTimestamp();
        int cancelledRequests;

        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_isDisposed, this);

            if (_currentJobCts is not null)
            {
                _currentJobCts.Cancel();
                _currentJobCts.Dispose();
                _cancelledRequests++;
            }

            _currentJobCts = linkedCts;
            cancelledRequests = _cancelledRequests;
        }

        SqlCompletionRequestContext effectiveRequest = request with { CancelledRequests = cancelledRequests };
        return ExecuteAsync(effectiveRequest, progress, linkedCts, cancellationToken, enqueueTimestamp);
    }

    public async ValueTask DisposeAsync()
    {
        CancellationTokenSource? current;

        lock (_gate)
        {
            if (_isDisposed)
                return;

            _isDisposed = true;
            current = _currentJobCts;
            _currentJobCts = null;
        }

        if (current is not null)
        {
            current.Cancel();
            current.Dispose();
        }

        await ValueTask.CompletedTask;
    }

    private Task<SqlCompletionStageSnapshot> ExecuteAsync(
        SqlCompletionRequestContext request,
        IProgress<SqlCompletionStageSnapshot>? progress,
        CancellationTokenSource linkedCts,
        CancellationToken externalCancellationToken,
        long enqueueTimestamp)
    {
        return Task.Run(() =>
        {
            try
            {
                long executionStartTimestamp = Stopwatch.GetTimestamp();
                long dispatchDelayMs = Math.Max(
                    0,
                    (long)Math.Round(Stopwatch.GetElapsedTime(enqueueTimestamp, executionStartTimestamp).TotalMilliseconds));

                var executionStopwatch = Stopwatch.StartNew();
                SqlCompletionStageSnapshot result = _engine.BuildCompletion(
                    request,
                    progress,
                    linkedCts.Token);

                executionStopwatch.Stop();
                long workerExecutionMs = Math.Max(0, (long)Math.Round(executionStopwatch.Elapsed.TotalMilliseconds));

                SqlCompletionTelemetry telemetry = result.Telemetry with
                {
                    WorkerDispatchDelayMs = dispatchDelayMs,
                    WorkerExecutionMs = workerExecutionMs,
                };

                return result with { Telemetry = telemetry };
            }
            catch (OperationCanceledException) when (linkedCts.IsCancellationRequested)
            {
                throw new TaskCanceledException();
            }
            finally
            {
                lock (_gate)
                {
                    if (ReferenceEquals(_currentJobCts, linkedCts))
                        _currentJobCts = null;
                }

                linkedCts.Dispose();
            }
        }, externalCancellationToken);
    }
}
