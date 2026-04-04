using Avalonia.Controls;
using System.Runtime.Serialization;
using VisualSqlArchitect.UI.Services;
using VisualSqlArchitect.UI.ViewModels;
using Xunit;

namespace VisualSqlArchitect.Tests.Unit.Services;

public class CommandPaletteFactoryNewCanvasTests
{
    [Fact]
    public void NewCanvasCommand_UsesInjectedCreateCanvasAction()
    {
#pragma warning disable SYSLIB0050
        var window = (Window)FormatterServices.GetUninitializedObject(typeof(Window));
#pragma warning restore SYSLIB0050
        var vm = new CanvasViewModel();
        var shell = new ShellViewModel(vm);
        var fileOps = new FileOperationsService(window, vm);
        var export = new ExportService(window, vm);
        var preview = new PreviewService(window, vm);

        bool invoked = false;
        var factory = new CommandPaletteFactory(
            window,
            () => shell.ActiveCanvas ?? vm,
            () => shell,
            fileOps,
            export,
            preview,
            () => invoked = true);
        var service = new CommandPaletteService(factory);

        service.Refresh();
        shell.SetCommandPalette(service.ViewModel);

        service.ViewModel.Open();
        var cmd = Assert.Single(service.ViewModel.Results, r => r.Shortcut == "Ctrl+N");
        cmd.Execute();

        Assert.True(invoked);
    }

    [Fact]
    public void TogglePreviewCommand_UsesShellOutputPreviewForActiveQueryCanvas()
    {
#pragma warning disable SYSLIB0050
        var window = (Window)FormatterServices.GetUninitializedObject(typeof(Window));
#pragma warning restore SYSLIB0050
        var vm = new CanvasViewModel();
        var shell = new ShellViewModel(vm);
        var fileOps = new FileOperationsService(window, vm);
        var export = new ExportService(window, vm);
        var preview = new PreviewService(window, vm);

        var factory = new CommandPaletteFactory(
            window,
            () => shell.ActiveCanvas ?? vm,
            () => shell,
            fileOps,
            export,
            preview
        );
        var service = new CommandPaletteService(factory);
        service.Refresh();
        shell.SetCommandPalette(service.ViewModel);

        var cmd = Assert.Single(service.ViewModel.Results, r => r.Shortcut == "F3");
        cmd.Execute();

        Assert.True(vm.DataPreview.IsVisible);
        Assert.True(shell.OutputPreview.IsVisible);
        Assert.Equal(OutputPreviewModalViewModel.EOutputPreviewMode.Query, shell.OutputPreview.Mode);
    }

    [Fact]
    public void ExplainCommand_RoutesToActiveDdlCanvasWhenDdlModeIsActive()
    {
#pragma warning disable SYSLIB0050
        var window = (Window)FormatterServices.GetUninitializedObject(typeof(Window));
#pragma warning restore SYSLIB0050
        var queryVm = new CanvasViewModel();
        var shell = new ShellViewModel(queryVm);
        var ddlVm = shell.EnsureDdlCanvas();
        shell.SetActiveMode(ShellViewModel.AppMode.Ddl);
        var fileOps = new FileOperationsService(window, queryVm, ddlVm);
        var export = new ExportService(window, queryVm);
        var preview = new PreviewService(window, queryVm);

        var factory = new CommandPaletteFactory(
            window,
            () => shell.ActiveCanvas ?? queryVm,
            () => shell,
            fileOps,
            export,
            preview
        );
        var service = new CommandPaletteService(factory);
        service.Refresh();
        shell.SetCommandPalette(service.ViewModel);

        var cmd = Assert.Single(service.ViewModel.Results, r => r.Shortcut == "F4");
        cmd.Execute();

        Assert.False(queryVm.ExplainPlan.IsVisible);
        Assert.True(ddlVm.ExplainPlan.IsVisible);
    }
}
