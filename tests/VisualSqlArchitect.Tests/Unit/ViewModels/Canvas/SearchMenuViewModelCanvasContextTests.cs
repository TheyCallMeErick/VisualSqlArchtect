using VisualSqlArchitect.UI.Services.Canvas.AutoJoin;
using VisualSqlArchitect.UI.Services.Explain;
using VisualSqlArchitect.UI.ViewModels;
using VisualSqlArchitect.UI.ViewModels.Canvas;
using VisualSqlArchitect.Nodes;

namespace VisualSqlArchitect.Tests.Unit.ViewModels.Canvas;

public class SearchMenuViewModelCanvasContextTests
{
    [Fact]
    public void DdlContext_JoinSearch_DoesNotReturnQueryJoinNodes()
    {
        var vm = new SearchMenuViewModel
        {
            CanvasContext = CanvasContext.Ddl,
            Query = "join",
        };

        Assert.All(vm.Results, r => Assert.True(r.IsTable || r.Definition.Category == NodeCategory.Ddl));
        Assert.DoesNotContain(vm.Results, r => !r.IsTable && r.Definition.Type == NodeType.Join);
    }

    [Fact]
    public void ContextChange_ClearsResidualQueryText()
    {
        var vm = new SearchMenuViewModel
        {
            Query = "table",
        };

        vm.CanvasContext = CanvasContext.Ddl;

        Assert.Equal(string.Empty, vm.Query);
    }
}


