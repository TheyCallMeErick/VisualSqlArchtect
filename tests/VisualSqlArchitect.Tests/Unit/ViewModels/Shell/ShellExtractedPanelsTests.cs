using VisualSqlArchitect.UI.ViewModels;

namespace VisualSqlArchitect.Tests.Unit.ViewModels.Shell;

public class ShellExtractedPanelsTests
{
    [Fact]
    public void ExtractedPanels_QueryMode_BindsAndShowsQueryScaffold()
    {
        var shell = new ShellViewModel();
        CanvasViewModel query = shell.EnsureCanvas();

        shell.SetActiveMode(ShellViewModel.AppMode.Query);

        Assert.Same(query.Sidebar, shell.LeftSidebar.QuerySidebar);
        Assert.Same(query.PropertyPanel, shell.RightSidebar.PropertyPanel);
        Assert.True(shell.LeftSidebar.IsVisible);
        Assert.True(shell.RightSidebar.IsVisible);
    }

    [Fact]
    public void ExtractedPanels_DdlMode_HidesSidebars()
    {
        var shell = new ShellViewModel();
        shell.EnsureCanvas();

        shell.SetActiveMode(ShellViewModel.AppMode.Ddl);

        Assert.False(shell.LeftSidebar.IsVisible);
        Assert.False(shell.RightSidebar.IsVisible);
        Assert.NotNull(shell.DdlCanvas);
    }

    [Fact]
    public void ExtractedPanels_ViewSubcanvas_KeepsExpectedCanvasContext()
    {
        var shell = new ShellViewModel();
        shell.EnsureCanvas();

        shell.SetActiveMode(ShellViewModel.AppMode.Ddl);
        shell.SetViewSubcanvasActive(true);

        Assert.Equal(CanvasContext.ViewSubcanvas, shell.ActiveCanvasContext);
    }

    [Fact]
    public void ExtractedPanels_ModeSwitch_ClearsViewSubcanvasResidualState()
    {
        var shell = new ShellViewModel();
        shell.EnsureCanvas();

        shell.SetActiveMode(ShellViewModel.AppMode.Ddl);
        shell.SetViewSubcanvasActive(true);

        shell.SetActiveMode(ShellViewModel.AppMode.Query);
        Assert.Equal(CanvasContext.Query, shell.ActiveCanvasContext);
        Assert.True(shell.LeftSidebar.IsVisible);
        Assert.True(shell.RightSidebar.IsVisible);

        shell.SetActiveMode(ShellViewModel.AppMode.Ddl);
        Assert.Equal(CanvasContext.Ddl, shell.ActiveCanvasContext);
        Assert.False(shell.LeftSidebar.IsVisible);
        Assert.False(shell.RightSidebar.IsVisible);
    }

    [Fact]
    public void ExtractedPanels_EmptyState_HidesSidebars()
    {
        var shell = new ShellViewModel();

        Assert.False(shell.LeftSidebar.IsVisible);
        Assert.False(shell.RightSidebar.IsVisible);
    }
}
