using System.IO;
using Xunit;

namespace VisualSqlArchitect.Tests.Unit.Controls;

public class SchemaControlTemplateRegressionTests
{
    [Fact]
    public void SchemaTemplate_DefinesInteractiveCardStates_AndFilterEmptyState()
    {
        string xaml = ReadSchemaXaml();

        Assert.Contains("Style Selector=\"Border.schema-header-card:pointerover\"", xaml);
        Assert.Contains("Style Selector=\"Border.schema-object-card:pointerover\"", xaml);
        Assert.Contains("Style Selector=\"Border.schema-object-card.expanded\"", xaml);
        Assert.Contains("IsVisible=\"{Binding ShowFilterEmptyState}\"", xaml);
        Assert.Contains("Nenhum objeto encontrado para o filtro atual", xaml);
    }

    [Fact]
    public void SchemaTemplate_UsesSuccessClassForAddNodeAction()
    {
        string xaml = ReadSchemaXaml();

        Assert.Contains("Command=\"{Binding AddNodeCommand}\"", xaml);
        Assert.Contains("Classes=\"success\"", xaml);
    }

    private static string ReadSchemaXaml()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            string candidate = Path.Combine(
                dir.FullName,
                "src",
                "VisualSqlArchitect.UI",
                "Controls",
                "SidebarLeft",
                "SchemaControl.axaml"
            );

            if (File.Exists(candidate))
                return File.ReadAllText(candidate);

            dir = dir.Parent;
        }

        throw new FileNotFoundException("Could not locate SchemaControl.axaml from test base directory.");
    }
}
