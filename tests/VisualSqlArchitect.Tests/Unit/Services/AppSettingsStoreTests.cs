using VisualSqlArchitect.UI.Services.Settings;

namespace VisualSqlArchitect.Tests.Unit.Services;

public class AppSettingsStoreTests
{
    [Fact]
    public void Load_NoFile_ReturnsDefaults()
    {
        string root = Path.Combine(Path.GetTempPath(), "vsa-settings-tests", Guid.NewGuid().ToString("N"));
        string file = Path.Combine(root, "app.settings.json");
        AppSettingsStore.SettingsPathOverride = file;

        try
        {
            AppSettings settings = AppSettingsStore.Load();
            Assert.Equal("Dark", settings.ThemeVariant);
        }
        finally
        {
            AppSettingsStore.SettingsPathOverride = null;
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Save_ThenLoad_RoundTripsThemeVariant()
    {
        string root = Path.Combine(Path.GetTempPath(), "vsa-settings-tests", Guid.NewGuid().ToString("N"));
        string file = Path.Combine(root, "app.settings.json");
        AppSettingsStore.SettingsPathOverride = file;

        try
        {
            AppSettingsStore.Save(new AppSettings { ThemeVariant = "Light" });
            AppSettings loaded = AppSettingsStore.Load();

            Assert.Equal("Light", loaded.ThemeVariant);
        }
        finally
        {
            AppSettingsStore.SettingsPathOverride = null;
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void SaveThemeVariant_Blank_FallsBackToDark()
    {
        string root = Path.Combine(Path.GetTempPath(), "vsa-settings-tests", Guid.NewGuid().ToString("N"));
        string file = Path.Combine(root, "app.settings.json");
        AppSettingsStore.SettingsPathOverride = file;

        try
        {
            AppSettingsStore.SaveThemeVariant("   ");
            AppSettings loaded = AppSettingsStore.Load();

            Assert.Equal("Dark", loaded.ThemeVariant);
        }
        finally
        {
            AppSettingsStore.SettingsPathOverride = null;
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }
}
