using DBWeaver.Metadata;

namespace DBWeaver.UI.Services.SqlEditor;

public interface ISqlCompletionEngine
{
    SqlCompletionStageSnapshot BuildCompletion(
        SqlCompletionRequestContext request,
        IProgress<SqlCompletionStageSnapshot>? progress = null,
        CancellationToken cancellationToken = default);

    SqlCompletionRequest GetSuggestions(
        string fullText,
        int caretOffset,
        DbMetadata? metadata,
        DatabaseProvider? provider = null,
        string? connectionProfileId = null,
        CancellationToken cancellationToken = default);
}
