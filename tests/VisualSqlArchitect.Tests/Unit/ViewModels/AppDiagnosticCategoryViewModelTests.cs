using VisualSqlArchitect.UI.Services.Benchmark;
using Material.Icons;
using VisualSqlArchitect.UI.ViewModels;

namespace VisualSqlArchitect.Tests.Unit.ViewModels;

public class AppDiagnosticCategoryViewModelTests
{
    [Fact]
    public void ReplaceItems_UpdatesCountsAndIssueFlags()
    {
        var vm = new AppDiagnosticCategoryViewModel
        {
            Key = "output",
            Title = "Output",
            IconKind = MaterialIconKind.CodeBraces,
        };

        vm.ReplaceItems(
        [
            new AppDiagnosticEntry { Name = "ok", Status = EDiagnosticStatus.Ok },
            new AppDiagnosticEntry { Name = "warn", Status = EDiagnosticStatus.Warning },
            new AppDiagnosticEntry { Name = "err", Status = EDiagnosticStatus.Error },
        ]);

        Assert.Equal(3, vm.TotalCount);
        Assert.Equal(1, vm.WarningCount);
        Assert.Equal(1, vm.ErrorCount);
        Assert.True(vm.HasIssues);
        Assert.True(vm.HasWarnings);
        Assert.True(vm.HasErrors);
    }

    [Fact]
    public void ReplaceItems_WithOnlyOkEntries_ClearsIssueFlags()
    {
        var vm = new AppDiagnosticCategoryViewModel();
        vm.ReplaceItems([new AppDiagnosticEntry { Name = "ok", Status = EDiagnosticStatus.Ok }]);

        Assert.Equal(1, vm.TotalCount);
        Assert.Equal(0, vm.WarningCount);
        Assert.Equal(0, vm.ErrorCount);
        Assert.False(vm.HasIssues);
        Assert.False(vm.HasWarnings);
        Assert.False(vm.HasErrors);
    }

    [Fact]
    public void IsExpanded_DefaultsTrue_AndCanToggle()
    {
        var vm = new AppDiagnosticCategoryViewModel();
        Assert.True(vm.IsExpanded);

        vm.IsExpanded = false;
        Assert.False(vm.IsExpanded);
    }
}

