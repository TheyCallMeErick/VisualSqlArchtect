using VisualSqlArchitect.Core;
using VisualSqlArchitect.Metadata;
using VisualSqlArchitect.UI.ViewModels.Canvas;

namespace VisualSqlArchitect.UI.Services.ConnectionManager;

public sealed class ConnectionActivationWorkflow : IConnectionActivationWorkflow
{
    public async Task<ConnectionActivationResult> ExecuteAsync(
        ConnectionProfile profile,
        SearchMenuViewModel? searchMenu,
        CanvasViewModel? canvas,
        Func<ConnectionConfig, SearchMenuViewModel, CancellationToken, Task<DbMetadata?>> loadMetadataAsync,
        CancellationToken ct)
    {
        if (searchMenu is null)
        {
            return new ConnectionActivationResult(
                Outcome: EConnectionActivationOutcome.SearchMenuUnavailable,
                FailureReason: "search menu not initialized");
        }

        ConnectionConfig config = profile.ToConnectionConfig();

        try
        {
            DbMetadata? metadata = await loadMetadataAsync(config, searchMenu, ct);
            if (canvas is not null && metadata is not null)
            {
                canvas.SetDatabaseContext(metadata, config);

                return new ConnectionActivationResult(
                    Outcome: EConnectionActivationOutcome.Connected,
                    Config: config,
                    Metadata: metadata,
                    ShouldOpenClearCanvasPrompt: !canvas.IsCanvasEmpty);
            }

            return new ConnectionActivationResult(
                Outcome: EConnectionActivationOutcome.MetadataUnavailable,
                Config: config);
        }
        catch (OperationCanceledException)
        {
            return new ConnectionActivationResult(EConnectionActivationOutcome.Cancelled, Config: config);
        }
        catch (Exception ex)
        {
            return new ConnectionActivationResult(
                Outcome: EConnectionActivationOutcome.Failed,
                Config: config,
                FailureException: ex);
        }
    }
}

