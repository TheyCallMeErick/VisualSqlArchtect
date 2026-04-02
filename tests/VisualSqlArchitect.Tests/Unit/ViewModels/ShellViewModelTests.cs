using VisualSqlArchitect.UI.ViewModels;
using Xunit;

namespace VisualSqlArchitect.Tests.Unit.ViewModels;

public class ShellViewModelTests
{
    [Fact]
    public void InitialState_StartVisible_AndCanvasNotInitialized()
    {
        var vm = new ShellViewModel();

        Assert.True(vm.IsStartVisible);
        Assert.False(vm.IsCanvasVisible);
        Assert.False(vm.IsConnectionManagerVisible);
        Assert.Null(vm.Canvas);
    }

    [Fact]
    public void EnterCanvas_InitializesCanvas_AndHidesStart()
    {
        var vm = new ShellViewModel();

        vm.EnterCanvas();

        Assert.False(vm.IsStartVisible);
        Assert.True(vm.IsCanvasVisible);
        Assert.NotNull(vm.Canvas);
    }

    [Fact]
    public void ReturnToStart_KeepsCanvasInstance()
    {
        var vm = new ShellViewModel();
        var canvas = vm.EnsureCanvas();

        vm.EnterCanvas();
        vm.ReturnToStart();

        Assert.True(vm.IsStartVisible);
        Assert.False(vm.IsCanvasVisible);
        Assert.Same(canvas, vm.Canvas);
    }

    [Fact]
    public void StartMenuCommandExecution_DoesNotInstantiateCanvasByItself()
    {
        var vm = new ShellViewModel();
        var called = false;

        vm.StartMenu.CreateNewDiagramRequested += () => called = true;
        vm.StartMenu.CreateNewDiagramCommand.Execute(null);

        Assert.True(called);
        Assert.Null(vm.Canvas);
        Assert.True(vm.IsStartVisible);
    }

    [Fact]
    public void ConnectionManagerVisibility_TracksCanvasConnectionManagerState()
    {
        var vm = new ShellViewModel();

        Assert.False(vm.IsConnectionManagerVisible);

        var canvas = vm.EnsureCanvas();
        Assert.False(vm.IsConnectionManagerVisible);

        canvas.ConnectionManager.IsVisible = true;
        Assert.True(vm.IsConnectionManagerVisible);

        canvas.ConnectionManager.IsVisible = false;
        Assert.False(vm.IsConnectionManagerVisible);
    }

    [Fact]
    public void SettingsVisibility_OpenAndClose_Works()
    {
        var vm = new ShellViewModel();

        Assert.False(vm.IsSettingsVisible);

        vm.OpenSettings();
        Assert.True(vm.IsSettingsVisible);

        vm.CloseSettings();
        Assert.False(vm.IsSettingsVisible);
    }

    [Fact]
    public void SettingsSectionSelection_SwitchesFlags()
    {
        var vm = new ShellViewModel();

        Assert.True(vm.IsAppearanceSectionSelected);
        Assert.Equal("Themes", vm.SettingsSectionTitle);

        vm.SelectSettingsSection(ShellViewModel.ESettingsSection.LanguageRegion);
        Assert.True(vm.IsLanguageRegionSectionSelected);
        Assert.False(vm.IsAppearanceSectionSelected);
        Assert.Equal("Language & Region", vm.SettingsSectionTitle);

        vm.SelectSettingsSection(ShellViewModel.ESettingsSection.Privacy);
        Assert.True(vm.IsPrivacySectionSelected);
        Assert.False(vm.IsLanguageRegionSectionSelected);
        Assert.Equal("Privacy", vm.SettingsSectionTitle);
        Assert.Equal("Trabalho em progresso.", vm.SettingsSectionSubtitle);
    }
}
