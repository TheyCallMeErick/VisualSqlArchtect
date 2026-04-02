using Material.Icons;

namespace VisualSqlArchitect.UI.ViewModels;

public sealed class PaletteCommandItem
{
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";
    public string Shortcut { get; init; } = "";
    public MaterialIconKind Icon { get; init; } = MaterialIconKind.Play;
    public string Tags { get; init; } = "";
    public Action Execute { get; init; } = () => { };
}
