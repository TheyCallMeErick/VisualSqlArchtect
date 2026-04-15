using AkkornStudio.Core;
using AkkornStudio.Metadata;
using AkkornStudio.UI.Services;
using AkkornStudio.UI.ViewModels.Canvas;

namespace AkkornStudio.UI.Services.ConnectionManager;

public interface IConnectionActivationWorkflow
{
    Task<ConnectionActivationResult> ExecuteAsync(
        ConnectionProfile profile,
        SearchMenuViewModel? searchMenu,
        CanvasViewModel? canvas,
        Func<ConnectionConfig, SearchMenuViewModel, CancellationToken, Task<DbMetadata?>> loadMetadataAsync,
        CancellationToken ct);
}

