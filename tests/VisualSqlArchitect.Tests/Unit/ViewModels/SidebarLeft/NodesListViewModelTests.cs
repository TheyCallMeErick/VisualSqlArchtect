using VisualSqlArchitect.UI.ViewModels;
using VisualSqlArchitect.Nodes;
using Xunit;

namespace VisualSqlArchitect.Tests.Unit.ViewModels.SidebarLeft;

public class NodesListViewModelTests
{
    [Fact]
    public void Constructor_PopulatesInitialGroups()
    {
        var vm = new NodesListViewModel((_, _) => { });

        Assert.NotEmpty(vm.FilteredGroups);
        Assert.True(vm.ShowIntro);
        Assert.All(vm.FilteredGroups, g => Assert.True(g.Items.Count > 0));
    }

    [Fact]
    public void SearchQuery_FiltersGroupsAndItems()
    {
        var vm = new NodesListViewModel((_, _) => { });

        vm.SearchQuery = "upper";

        Assert.False(vm.ShowIntro);
        Assert.NotEmpty(vm.FilteredGroups);
        Assert.All(vm.FilteredGroups, group =>
            Assert.All(group.Items, item =>
                Assert.True(
                    item.Title.Contains("upper", StringComparison.OrdinalIgnoreCase)
                    || item.Subtitle.Contains("upper", StringComparison.OrdinalIgnoreCase)
                )
            )
        );
    }

    [Fact]
    public void SearchQuery_NoMatches_ProducesEmptyGroups()
    {
        var vm = new NodesListViewModel((_, _) => { });

        vm.SearchQuery = "__definitely_no_match__";

        Assert.False(vm.ShowIntro);
        Assert.Empty(vm.FilteredGroups);
    }

    [Fact]
    public void ClearSearchCommand_RestoresIntroAndGroups()
    {
        var vm = new NodesListViewModel((_, _) => { });
        int initialCount = vm.FilteredGroups.Count;

        vm.SearchQuery = "upper";
        Assert.False(vm.ShowIntro);

        vm.ClearSearchCommand.Execute(null);

        Assert.True(vm.ShowIntro);
        Assert.Equal(string.Empty, vm.SearchQuery);
        Assert.Equal(initialCount, vm.FilteredGroups.Count);
    }

    [Fact]
    public void CanvasContext_Ddl_ShowsOnlyDdlCategory()
    {
        var vm = new NodesListViewModel((_, _) => { });

        vm.CanvasContext = CanvasContext.Ddl;

        Assert.NotEmpty(vm.FilteredGroups);
        Assert.All(vm.FilteredGroups, g => Assert.Equal(NodeCategory.Ddl, g.Category));
        Assert.Contains(vm.FilteredGroups, g => g.Name == "Definitions");
        Assert.Contains(vm.FilteredGroups, g => g.Name == "Outputs");
    }

    [Fact]
    public void CanvasContext_Transition_ClearsResidualSearch()
    {
        var vm = new NodesListViewModel((_, _) => { });
        vm.SearchQuery = "join";

        vm.CanvasContext = CanvasContext.Ddl;

        Assert.Equal(string.Empty, vm.SearchQuery);
        Assert.True(vm.ShowIntro);
    }
}
