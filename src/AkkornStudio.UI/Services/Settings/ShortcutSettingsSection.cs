using AkkornStudio.UI.Services.Input.ShortcutRegistry;

namespace AkkornStudio.UI.Services.Settings;

/// <summary>
/// Persisted shortcut customization section inside app settings JSON.
/// </summary>
public sealed class ShortcutSettingsSection
{
    public int Version { get; set; } = 1;
    public List<ShortcutCustomizationEntry> Overrides { get; set; } = [];
}
