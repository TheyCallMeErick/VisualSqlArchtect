using AkkornStudio.Core;
using AkkornStudio.Metadata;

namespace AkkornStudio.UI.Services.ConnectionManager;

public interface IConnectionCanvasPromptCoordinator
{
    ConnectionCanvasPromptState Open(DbMetadata metadata, ConnectionConfig config);

    ConnectionCanvasPromptState Close();

    bool ShouldAddDismissWarning(bool dismissedByUser, bool isPromptVisible, DbMetadata? pendingMetadata);
}

