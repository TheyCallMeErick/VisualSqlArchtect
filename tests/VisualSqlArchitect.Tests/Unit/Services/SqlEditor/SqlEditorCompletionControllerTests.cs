using DBWeaver.Core;
using DBWeaver.UI.Services.SqlEditor;

namespace DBWeaver.Tests.Unit.Services.SqlEditor;

public sealed class SqlEditorCompletionControllerTests
{
    [Fact]
    public void GetCompletionRequest_WithKeywordPrefix_ReturnsKeywordSuggestion()
    {
        var sut = new SqlEditorCompletionController();

        SqlCompletionRequest request = sut.GetCompletionRequest(
            fullText: "SEL",
            caretOffset: 3,
            metadata: null,
            provider: DatabaseProvider.Postgres,
            connectionProfileId: null);

        Assert.Equal(3, request.PrefixLength);
        Assert.Contains(request.Suggestions, suggestion =>
            suggestion.Kind == SqlCompletionKind.Keyword
            && suggestion.Label == "SELECT");
    }

    [Fact]
    public void TryResolveSignatureHelp_WithFunctionCall_ReturnsSignature()
    {
        var sut = new SqlEditorCompletionController();
        const string sql = "SELECT DATE_TRUNC('day', NOW())";
        int caretOffset = sql.IndexOf("NOW", StringComparison.Ordinal);

        SignatureHelpInfo? help = sut.TryResolveSignatureHelp(
            fullText: sql,
            caretOffset: caretOffset,
            provider: DatabaseProvider.Postgres);

        Assert.NotNull(help);
        Assert.Equal("DATE_TRUNC", help!.Signature.Name);
        Assert.False(string.IsNullOrWhiteSpace(help!.DisplayText));
    }
}
