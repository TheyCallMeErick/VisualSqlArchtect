using VisualSqlArchitect.UI.Services.Settings;
using VisualSqlArchitect.UI.ViewModels;
using Xunit;

namespace VisualSqlArchitect.Tests.Unit.Services;

public class SettingsWorkspaceModuleTests
{
    [Fact]
    public void OpenSettings_FromStart_KeepsStartVisible()
    {
        var shell = new ShellViewModel();
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
        var shell = new ShellViewModel();
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
        var shell = new ShellViewModel();
        shell.OpenSettings();

        var module = new SettingsWorkspaceModule(
            getShell: () => shell,
            enterCanvas: () => { }
        );

        module.CloseSettings();

        Assert.False(shell.IsSettingsVisible);
    }
}
