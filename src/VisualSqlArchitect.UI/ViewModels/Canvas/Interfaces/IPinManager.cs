namespace VisualSqlArchitect.UI.ViewModels.Canvas;

public interface IPinManager
{
    void ConnectPins(PinViewModel from, PinViewModel to);

    void DeleteConnection(ConnectionViewModel conn);

    void ClearNarrowingIfNeeded(IEnumerable<NodeViewModel> nodes);
}
