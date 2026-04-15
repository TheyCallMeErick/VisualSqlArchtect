using DBWeaver.Metadata;

namespace DBWeaver.UI.Services.SqlEditor;

public sealed record SqlCompletionRequestContext(
    string FullText,
    int CaretOffset,
    DbMetadata? Metadata,
    DatabaseProvider Provider,
    string? ConnectionProfileId,
    int CancelledRequests = 0);
