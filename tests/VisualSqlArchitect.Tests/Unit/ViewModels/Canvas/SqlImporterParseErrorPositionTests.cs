using VisualSqlArchitect.UI.Services.Canvas.AutoJoin;
using VisualSqlArchitect.UI.Services.Explain;
using VisualSqlArchitect.UI.ViewModels;
using Xunit;

namespace VisualSqlArchitect.Tests.Unit.ViewModels.Canvas;

public class SqlImporterParseErrorPositionTests
{
    [Fact]
    public async Task ImportAsync_WithUnmatchedParenthesis_ShowsSyntaxPosition()
    {
        var canvas = new CanvasViewModel();

        canvas.SqlImporter.ImportStartDelayMs = 0;
        canvas.SqlImporter.ImportTimeout = TimeSpan.FromSeconds(5);
        canvas.SqlImporter.SqlInput = "SELECT id FROM orders WHERE (id = 1";

        await canvas.SqlImporter.ImportAsync();

        Assert.False(string.IsNullOrWhiteSpace(canvas.SqlImporter.StatusMessage));
        Assert.True(
            canvas.SqlImporter.StatusMessage.Contains("line", StringComparison.OrdinalIgnoreCase)
            || canvas.SqlImporter.StatusMessage.Contains("linha", StringComparison.OrdinalIgnoreCase)
            || canvas.SqlImporter.StatusMessage.Contains("lÃ­nea", StringComparison.OrdinalIgnoreCase)
            || canvas.SqlImporter.StatusMessage.Contains("ÑÑ‚Ñ€Ð¾Ðº", StringComparison.OrdinalIgnoreCase)
            || canvas.SqlImporter.StatusMessage.Contains("è¡Œ", StringComparison.Ordinal)
        );
        Assert.True(
            canvas.SqlImporter.StatusMessage.Contains("column", StringComparison.OrdinalIgnoreCase)
            || canvas.SqlImporter.StatusMessage.Contains("coluna", StringComparison.OrdinalIgnoreCase)
            || canvas.SqlImporter.StatusMessage.Contains("columna", StringComparison.OrdinalIgnoreCase)
            || canvas.SqlImporter.StatusMessage.Contains("ÑÑ‚Ð¾Ð»Ð±", StringComparison.OrdinalIgnoreCase)
            || canvas.SqlImporter.StatusMessage.Contains("åˆ—", StringComparison.Ordinal)
        );
    }

    [Fact]
    public async Task ImportAsync_WithUnterminatedString_ShowsSyntaxPosition()
    {
        var canvas = new CanvasViewModel();

        canvas.SqlImporter.ImportStartDelayMs = 0;
        canvas.SqlImporter.ImportTimeout = TimeSpan.FromSeconds(5);
        canvas.SqlImporter.SqlInput = "SELECT id FROM orders WHERE note = 'abc";

        await canvas.SqlImporter.ImportAsync();

        Assert.False(string.IsNullOrWhiteSpace(canvas.SqlImporter.StatusMessage));
        Assert.True(
            canvas.SqlImporter.StatusMessage.Contains("line", StringComparison.OrdinalIgnoreCase)
            || canvas.SqlImporter.StatusMessage.Contains("linha", StringComparison.OrdinalIgnoreCase)
            || canvas.SqlImporter.StatusMessage.Contains("lÃ­nea", StringComparison.OrdinalIgnoreCase)
            || canvas.SqlImporter.StatusMessage.Contains("ÑÑ‚Ñ€Ð¾Ðº", StringComparison.OrdinalIgnoreCase)
            || canvas.SqlImporter.StatusMessage.Contains("è¡Œ", StringComparison.Ordinal)
        );
        Assert.True(
            canvas.SqlImporter.StatusMessage.Contains("column", StringComparison.OrdinalIgnoreCase)
            || canvas.SqlImporter.StatusMessage.Contains("coluna", StringComparison.OrdinalIgnoreCase)
            || canvas.SqlImporter.StatusMessage.Contains("columna", StringComparison.OrdinalIgnoreCase)
            || canvas.SqlImporter.StatusMessage.Contains("ÑÑ‚Ð¾Ð»Ð±", StringComparison.OrdinalIgnoreCase)
            || canvas.SqlImporter.StatusMessage.Contains("åˆ—", StringComparison.Ordinal)
        );
    }
}


