using DBWeaver.Core;
using DBWeaver.Metadata;

namespace DBWeaver.UI.Services.SqlEditor;

public sealed class SqlEditorCompletionController
{
    private readonly ISqlCompletionEngine _completionEngine;
    private readonly SqlCompletionProvider _completionProvider;
    private readonly ISqlCompletionWorker _completionWorker;
    private readonly SqlSignatureHelpService _signatureHelpService;
    private readonly SqlHoverDocumentationService _hoverDocumentationService;

    public SqlEditorCompletionController(
        SqlCompletionProvider? completionProvider = null,
        ISqlCompletionWorker? completionWorker = null,
        SqlSignatureHelpService? signatureHelpService = null,
        SqlHoverDocumentationService? hoverDocumentationService = null)
    {
        _completionProvider = completionProvider ?? new SqlCompletionProvider();
        _completionEngine = _completionProvider;
        _completionWorker = completionWorker ?? new SqlCompletionWorker(_completionEngine);
        _signatureHelpService = signatureHelpService ?? new SqlSignatureHelpService();
        _hoverDocumentationService = hoverDocumentationService ?? new SqlHoverDocumentationService();
    }

    public Task<SqlCompletionStageSnapshot> RequestCompletionAsync(
        string fullText,
        int caretOffset,
        DbMetadata? metadata,
        DatabaseProvider provider,
        string? connectionProfileId,
        IProgress<SqlCompletionStageSnapshot>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var request = new SqlCompletionRequestContext(
            fullText,
            caretOffset,
            metadata,
            provider,
            connectionProfileId);

        return _completionWorker.RequestAsync(request, progress, cancellationToken);
    }

    public SqlCompletionRequest GetCompletionRequest(
        string fullText,
        int caretOffset,
        DbMetadata? metadata,
        DatabaseProvider provider,
        string? connectionProfileId)
    {
        return _completionEngine.GetSuggestions(
            fullText,
            caretOffset,
            metadata,
            provider,
            connectionProfileId);
    }

    public void RecordSuggestionAccepted(string suggestionLabel, string? connectionProfileId)
    {
        _completionProvider.RecordAcceptedSuggestion(suggestionLabel, connectionProfileId);
    }

    public SignatureHelpInfo? TryResolveSignatureHelp(string fullText, int caretOffset, DatabaseProvider provider)
    {
        return _signatureHelpService.TryResolve(fullText, caretOffset, provider);
    }

    public HoverDocumentationInfo? TryResolveHoverDocumentation(
        string fullText,
        int caretOffset,
        DbMetadata? metadata,
        DatabaseProvider provider)
    {
        return _hoverDocumentationService.TryResolve(fullText, caretOffset, metadata, provider);
    }
}
