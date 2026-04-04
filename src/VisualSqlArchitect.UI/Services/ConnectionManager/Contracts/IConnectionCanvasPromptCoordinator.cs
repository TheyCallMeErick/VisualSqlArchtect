using VisualSqlArchitect.Core;
using VisualSqlArchitect.Metadata;

namespace VisualSqlArchitect.UI.Services.ConnectionManager;

public interface IConnectionCanvasPromptCoordinator
{
    ConnectionCanvasPromptState Open(DbMetadata metadata, ConnectionConfig config);

    ConnectionCanvasPromptState Close();

    bool ShouldAddDismissWarning(bool dismissedByUser, bool isPromptVisible, DbMetadata? pendingMetadata);
}

