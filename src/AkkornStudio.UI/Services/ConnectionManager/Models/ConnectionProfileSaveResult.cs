namespace AkkornStudio.UI.Services.ConnectionManager;

public readonly record struct ConnectionProfileSaveResult(
    ConnectionProfile SelectedProfile,
    bool ActiveProfileAffected);

