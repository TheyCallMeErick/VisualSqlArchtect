using VisualSqlArchitect.UI.ViewModels;

namespace VisualSqlArchitect.UI.Services;

public interface ICommandPaletteService
{
    CommandPaletteViewModel ViewModel { get; }
    void Refresh();
}
