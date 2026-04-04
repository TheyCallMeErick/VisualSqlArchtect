using VisualSqlArchitect.UI.ViewModels;

namespace VisualSqlArchitect.UI.Services;

/// <summary>
/// Singleton-safe provider that delegates canvas resolution to a caller-supplied func.
/// A single instance can be shared across multiple consumers and will always
/// return whichever canvas is currently active without capturing a stale reference.
/// </summary>
public sealed class ActiveCanvasProvider : IActiveCanvasProvider
{
    private readonly Func<CanvasViewModel> _resolve;

    public ActiveCanvasProvider(Func<CanvasViewModel> resolve)
    {
        _resolve = resolve ?? throw new ArgumentNullException(nameof(resolve));
    }

    public CanvasViewModel GetActive() => _resolve();
}
