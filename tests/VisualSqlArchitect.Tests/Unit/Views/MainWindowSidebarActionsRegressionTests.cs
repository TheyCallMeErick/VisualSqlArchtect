using System.IO;
using Xunit;

namespace VisualSqlArchitect.Tests.Unit.Views;

public class MainWindowSidebarActionsRegressionTests
{
    [Fact]
    public void MainWindow_WiresSidebarActions_ToSearchAndConnectionsPanel()
    {
        string source = ReadMainWindowCodeBehind();

        Assert.Contains("sidebar.AddNodeRequested += () =>", source);
        Assert.Contains("OpenSearch();", source);

        Assert.Contains("sidebar.AddConnectionRequested += () => OpenConnectionsPanel(beginNewProfile: true, keepStartVisible: false);", source);
        Assert.Contains("private ConnectionWorkspaceModule GetConnectionModule()", source);
        Assert.Contains("GetConnectionModule().OpenManager(beginNewProfile, keepStartVisible);", source);
    }

    [Fact]
    public void MainWindow_UsesOpenConnectionsPanel_ForStartAndHeaderFlows()
    {
        string source = ReadMainWindowCodeBehind();

        Assert.Contains("private void OnStartOpenConnectionsRequested()", source);
        Assert.Contains("OpenConnectionsPanel(beginNewProfile: true, keepStartVisible: true);", source);

        Assert.DoesNotContain("B(\"ConnectionBadgeBtn\", () =>", source);
        Assert.Contains("CurrentShell.StartMenu.OpenSavedConnectionRequested += OnStartOpenSavedConnectionRequested;", source);
        Assert.Contains("vm.ConnectionManager.IsVisible = false;", source);
        Assert.Contains("CurrentVm.ConnectionManager.IsVisible = false;", source);
    }

    private static string ReadMainWindowCodeBehind()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            string candidate = Path.Combine(
                dir.FullName,
                "src",
                "VisualSqlArchitect.UI",
                "Views",
                "Shell",
                "MainWindow.axaml.cs"
            );

            if (File.Exists(candidate))
                return File.ReadAllText(candidate);

            dir = dir.Parent;
        }

        throw new FileNotFoundException("Could not locate MainWindow.axaml.cs from test base directory.");
    }
}
