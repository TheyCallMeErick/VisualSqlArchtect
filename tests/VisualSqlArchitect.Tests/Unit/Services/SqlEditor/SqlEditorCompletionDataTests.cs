using DBWeaver.UI.Services.SqlEditor;
using Material.Icons;

namespace DBWeaver.Tests.Unit.Services.SqlEditor;

public sealed class SqlEditorCompletionDataTests
{
    [Theory]
    [InlineData(SqlCompletionKind.Keyword, MaterialIconKind.CodeBraces)]
    [InlineData(SqlCompletionKind.Table, MaterialIconKind.Table)]
    [InlineData(SqlCompletionKind.Column, MaterialIconKind.TableColumn)]
    [InlineData(SqlCompletionKind.Function, MaterialIconKind.Function)]
    [InlineData(SqlCompletionKind.Snippet, MaterialIconKind.ContentPaste)]
    [InlineData(SqlCompletionKind.Join, MaterialIconKind.SetMerge)]
    public void Constructor_AssignsIconKindBySuggestionType(SqlCompletionKind kind, MaterialIconKind expectedIcon)
    {
        var sut = new SqlEditorCompletionData(
            label: "item",
            insertText: "item",
            description: "desc",
            kind: kind,
            prefixLength: 0);

        var content = Assert.IsType<SqlEditorCompletionItemContent>(sut.Content);
        Assert.Equal(kind, content.Kind);
        Assert.Equal(expectedIcon, content.IconKind);
    }
}
