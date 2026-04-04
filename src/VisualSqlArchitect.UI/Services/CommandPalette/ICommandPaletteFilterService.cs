namespace VisualSqlArchitect.UI.Services.CommandPalette;

public interface ICommandPaletteFilterService
{
    IReadOnlyList<PaletteCommandItem> FilterAndSort(
        IEnumerable<PaletteCommandItem> commands,
        string query);
}

