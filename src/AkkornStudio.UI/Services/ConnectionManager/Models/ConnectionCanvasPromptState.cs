using AkkornStudio.Core;
using AkkornStudio.Metadata;

namespace AkkornStudio.UI.Services.ConnectionManager;

public readonly record struct ConnectionCanvasPromptState(
    bool IsVisible,
    DbMetadata? PendingMetadata,
    ConnectionConfig? PendingConfig);

