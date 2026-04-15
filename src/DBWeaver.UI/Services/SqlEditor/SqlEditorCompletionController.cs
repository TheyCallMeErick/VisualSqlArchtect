using DBWeaver.Core;
using DBWeaver.Metadata;

namespace DBWeaver.UI.Services.SqlEditor;

public sealed class SqlEditorCompletionController
{
    private readonly SqlCompletionProvider _completionProvider;
    private readonly SqlSignatureHelpService _signatureHelpService;
    private readonly SqlHoverDocumentationService _hoverDocumentationService;

    public SqlEditorCompletionController(
        SqlCompletionProvider? completionProvider = null,
        SqlSignatureHelpService? signatureHelpService = null,
        SqlHoverDocumentationService? hoverDocumentationService = null)
    {
        _completionProvider = completionProvider ?? new SqlCompletionProvider();
        _signatureHelpService = signatureHelpService ?? new SqlSignatureHelpService();
        _hoverDocumentationService = hoverDocumentationService ?? new SqlHoverDocumentationService();
    }

    public SqlCompletionRequest GetCompletionRequest(
        string fullText,
        int caretOffset,
        DbMetadata? metadata,
        DatabaseProvider provider,
        string? connectionProfileId)
    {
        return _completionProvider.GetSuggestions(
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
