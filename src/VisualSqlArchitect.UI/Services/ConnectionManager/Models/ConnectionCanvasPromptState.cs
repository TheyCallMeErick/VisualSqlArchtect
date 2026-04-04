using VisualSqlArchitect.Core;
using VisualSqlArchitect.Metadata;

namespace VisualSqlArchitect.UI.Services.ConnectionManager;

public readonly record struct ConnectionCanvasPromptState(
    bool IsVisible,
    DbMetadata? PendingMetadata,
    ConnectionConfig? PendingConfig);

