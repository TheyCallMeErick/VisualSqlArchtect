using System.Text.Json;

namespace VisualSqlArchitect.UI.Services.Settings;

public sealed class AppSettings
{
    public string ThemeVariant { get; set; } = "Dark";
}

public static class AppSettingsStore
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public static string? SettingsPathOverride { get; set; }

    private static string SettingsPath => SettingsPathOverride ?? Path.Combine(
        global::VisualSqlArchitect.UI.AppConstants.AppDataDirectory,
        "app.settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsPath))
                return new AppSettings();

            string json = File.ReadAllText(SettingsPath);
            return JsonSerializer.Deserialize<AppSettings>(json, JsonOpts) ?? new AppSettings();
        }
        catch (Exception ex) when (ex is IOException or JsonException or InvalidOperationException)
        {
            return new AppSettings();
        }
    }

    public static void Save(AppSettings settings)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
            string json = JsonSerializer.Serialize(settings, JsonOpts);
            File.WriteAllText(SettingsPath, json);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            // best effort
        }
    }

    public static void SaveThemeVariant(string variant)
    {
        AppSettings settings = Load();
        settings.ThemeVariant = string.IsNullOrWhiteSpace(variant) ? "Dark" : variant;
        Save(settings);
    }
}
