using VisualSqlArchitect.Core;
using VisualSqlArchitect.UI.Services;
using VisualSqlArchitect.UI.Services.Localization;

namespace VisualSqlArchitect.UI.Services.ConnectionManager;

public sealed class ConnectionProfileFormMapper(ILocalizationService localization) : IConnectionProfileFormMapper
{
    private readonly ILocalizationService _localization = localization;

    public ConnectionProfileFormData CreateNew() =>
        new(
            Id: Guid.NewGuid().ToString(),
            Name: _localization["connection.new"],
            Provider: DatabaseProvider.Postgres,
            Host: AppConstants.DefaultHost,
            Port: ConnectionProfile.DefaultPort(DatabaseProvider.Postgres),
            Database: string.Empty,
            Username: string.Empty,
            Password: string.Empty,
            UseIntegratedSecurity: false,
            TimeoutSeconds: 30);

    public ConnectionProfileFormData FromProfile(ConnectionProfile profile) =>
        new(
            Id: profile.Id,
            Name: profile.Name,
            Provider: profile.Provider,
            Host: profile.Host,
            Port: profile.Port,
            Database: profile.Database,
            Username: profile.Username,
            Password: profile.Password,
            UseIntegratedSecurity: profile.UseIntegratedSecurity,
            TimeoutSeconds: profile.TimeoutSeconds);

    public ConnectionProfile ToProfile(ConnectionProfileFormData formData) =>
        new()
        {
            Id = formData.Id,
            Name = formData.Name,
            Provider = formData.Provider,
            Host = formData.Host,
            Port = formData.Port,
            Database = formData.Database,
            Username = formData.Username,
            Password = formData.Password,
            UseIntegratedSecurity = formData.UseIntegratedSecurity,
            TimeoutSeconds = formData.TimeoutSeconds,
        };
}

