using AkkornStudio.UI.ViewModels;

namespace AkkornStudio.UI.Services.CommandPalette;

public interface ICommandPaletteService
{
    CommandPaletteViewModel ViewModel { get; }
    void Refresh();
}
