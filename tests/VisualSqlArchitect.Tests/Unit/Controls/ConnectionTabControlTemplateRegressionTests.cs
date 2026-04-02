using System.IO;
using Xunit;

namespace VisualSqlArchitect.Tests.Unit.Controls;

public class ConnectionTabControlTemplateRegressionTests
{
    [Fact]
    public void ConnectionTemplate_DefinesInteractiveCardStates_AndEmptyStateHint()
    {
        string xaml = ReadConnectionXaml();

        Assert.Contains("Style Selector=\"Border.connection-card:pointerover\"", xaml);
        Assert.Contains("Style Selector=\"Border.connection-card.active\"", xaml);
        Assert.Contains("Classes=\"empty-state\"", xaml);
        Assert.Contains("Profiles.Count, Converter={x:Static BoolConverters.Not}", xaml);
    }

    [Fact]
    public void ConnectionTemplate_UsesPrimaryClassForNewConnectionCta()
    {
        string xaml = ReadConnectionXaml();

        Assert.Contains("Content=\"{Binding [connectionTab.new], Source={x:Static loc:LocalizationService.Instance}}\"", xaml);
        Assert.Contains("Classes=\"primary\"", xaml);
        Assert.Contains("Command=\"{Binding OpenNewProfileCommand}\"", xaml);
    }

    private static string ReadConnectionXaml()
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
                "ConnectionTabControl.axaml"
            );

            if (File.Exists(candidate))
                return File.ReadAllText(candidate);

            dir = dir.Parent;
        }

        throw new FileNotFoundException("Could not locate ConnectionTabControl.axaml from test base directory.");
    }
}
