using VisualSqlArchitect.Core;
using VisualSqlArchitect.Metadata;
using VisualSqlArchitect.UI.Services;
using VisualSqlArchitect.UI.ViewModels.Canvas;

namespace VisualSqlArchitect.UI.Services.ConnectionManager;

public interface IConnectionActivationWorkflow
{
    Task<ConnectionActivationResult> ExecuteAsync(
        ConnectionProfile profile,
        SearchMenuViewModel? searchMenu,
        CanvasViewModel? canvas,
        Func<ConnectionConfig, SearchMenuViewModel, CancellationToken, Task<DbMetadata?>> loadMetadataAsync,
        CancellationToken ct);
}

