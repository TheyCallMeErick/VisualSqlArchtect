using AkkornStudio.UI.Services.Settings;
using AkkornStudio.UI.ViewModels;
using Xunit;

namespace AkkornStudio.Tests.Unit.Services;

public class SettingsWorkspaceModuleTests
{
    [Fact]
    public void OpenSettings_FromStart_KeepsStartVisible()
    {
        var shell = new ShellViewModel(connectionManagerViewModelFactory: global::AkkornStudio.UI.Services.ConnectionManager.ConnectionManagerViewModelFactory.CreateDefault());
        var enteredCanvas = false;

        var module = new SettingsWorkspaceModule(
            getShell: () => shell,
            enterCanvas: () => enteredCanvas = true
        );

        module.OpenSettings(keepStartVisible: true);

        Assert.True(shell.IsSettingsVisible);
        Assert.False(enteredCanvas);
    }

    [Fact]
    public void OpenSettings_FromEditor_EntersCanvas()
    {
        var shell = new ShellViewModel(connectionManagerViewModelFactory: global::AkkornStudio.UI.Services.ConnectionManager.ConnectionManagerViewModelFactory.CreateDefault());
        var enteredCanvas = false;

        var module = new SettingsWorkspaceModule(
            getShell: () => shell,
            enterCanvas: () => enteredCanvas = true
        );

        module.OpenSettings(keepStartVisible: false);

        Assert.True(shell.IsSettingsVisible);
        Assert.True(enteredCanvas);
    }

    [Fact]
    public void CloseSettings_HidesModal()
    {
        var shell = new ShellViewModel(connectionManagerViewModelFactory: global::AkkornStudio.UI.Services.ConnectionManager.ConnectionManagerViewModelFactory.CreateDefault());
        shell.OpenSettings();

        var module = new SettingsWorkspaceModule(
            getShell: () => shell,
            enterCanvas: () => { }
        );

        module.CloseSettings();

        Assert.False(shell.IsSettingsVisible);
    }
}
