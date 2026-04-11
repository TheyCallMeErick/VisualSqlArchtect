using System.IO;

namespace DBWeaver.Tests.Unit.Controls;

public sealed class AppCompletionTemplateRegressionTests
{
    [Fact]
    public void AppTemplate_CompletionItems_RenderTypeIconAndText()
    {
        string xaml = ReadXaml();

        Assert.Contains("DataType=\"sqlcomp:SqlEditorCompletionItemContent\"", xaml);
        Assert.Contains("<mi:MaterialIcon Kind=\"{Binding IconKind}\"", xaml);
        Assert.Contains("Text=\"{Binding Label}\"", xaml);
        Assert.Contains("Text=\"{Binding Description}\"", xaml);
    }

    private static string ReadXaml()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            string candidate = Path.Combine(
                dir.FullName,
                "src",
                "DBWeaver.UI",
                "App.axaml");

            if (File.Exists(candidate))
                return File.ReadAllText(candidate);

            dir = dir.Parent;
        }

        throw new FileNotFoundException("Could not locate App.axaml from test base directory.");
    }
}
