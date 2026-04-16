using Avalonia.Controls;
using AkkornStudio.UI.Services.SqlEditor;

namespace AkkornStudio.Tests.Unit.Services.SqlEditor;

public sealed class SqlEditorCompletionLoadingDataTests
{
    [Fact]
    public void Constructor_BuildsVisibleContentText()
    {
        var sut = new SqlEditorCompletionLoadingData("Carregando...");

        Assert.Equal("Carregando...", sut.Text);
        Assert.Equal("Carregando...", sut.Description);
        _ = Assert.IsType<TextBlock>(sut.Content);
    }

    [Fact]
    public void Constructor_WithEmptyMessage_UsesDefaultFallbackText()
    {
        var sut = new SqlEditorCompletionLoadingData(string.Empty);

        Assert.Equal("Carregando sugestoes...", sut.Text);
        Assert.Equal("Carregando sugestoes...", sut.Description);
    }

    [Fact]
    public void Complete_DoesNotMutateDocument()
    {
        var sut = new SqlEditorCompletionLoadingData("Carregando...");

        sut.Complete(null!, null!, EventArgs.Empty);

        Assert.Equal("Carregando...", sut.Text);
    }
}
