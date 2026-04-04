using VisualSqlArchitect.Core;

namespace VisualSqlArchitect.UI.Services.ConnectionManager;

public readonly record struct ConnectionProfileFormData(
    string Id,
    string Name,
    DatabaseProvider Provider,
    string Host,
    int Port,
    string Database,
    string Username,
    string Password,
    bool UseIntegratedSecurity,
    int TimeoutSeconds);

