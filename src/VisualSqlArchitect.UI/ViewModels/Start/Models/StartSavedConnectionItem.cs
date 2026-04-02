namespace VisualSqlArchitect.UI.ViewModels;

public sealed record StartSavedConnectionItem(
    string Id,
    string Name,
    string Provider,
    string StatusLabel,
    bool IsConnected
);
