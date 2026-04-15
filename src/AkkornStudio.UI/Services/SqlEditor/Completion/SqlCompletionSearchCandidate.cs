namespace AkkornStudio.UI.Services.SqlEditor;

public sealed record SqlCompletionSearchCandidate(
    string NormalizedText,
    SqlCompletionSuggestion Suggestion,
    SqlCompletionKind Kind,
    string? TableFullName = null,
    string? SchemaName = null);
