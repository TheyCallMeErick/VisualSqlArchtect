namespace DBWeaver.UI.Services.SqlEditor;

public interface ISqlCompletionWorker : IAsyncDisposable
{
    Task<SqlCompletionStageSnapshot> RequestAsync(
        SqlCompletionRequestContext request,
        IProgress<SqlCompletionStageSnapshot>? progress = null,
        CancellationToken cancellationToken = default);
}
