using DBWeaver.UI.Services.SqlEditor;
using Avalonia.Controls;
using Material.Icons;
using Material.Icons.Avalonia;

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

        var row = Assert.IsType<StackPanel>(sut.Content);
        var iconHost = Assert.IsType<Border>(row.Children[0]);
        var icon = Assert.IsType<MaterialIcon>(iconHost.Child);
        Assert.Equal(expectedIcon, icon.Kind);
    }

    [Fact]
    public void Constructor_WithEmptyLabel_UsesInsertTextAsFallbackLabel()
    {
        var sut = new SqlEditorCompletionData(
            label: string.Empty,
            insertText: "SELECT",
            description: "keyword",
            kind: SqlCompletionKind.Keyword,
            prefixLength: 0);

        Assert.EndsWith("SELECT", sut.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void Constructor_WithEmptyLabelAndInsert_UsesDescriptionAsFallbackLabel()
    {
        var sut = new SqlEditorCompletionData(
            label: string.Empty,
            insertText: string.Empty,
            description: "descr",
            kind: SqlCompletionKind.Keyword,
            prefixLength: 0);

        Assert.EndsWith("descr", sut.Text, StringComparison.Ordinal);
    }
}
