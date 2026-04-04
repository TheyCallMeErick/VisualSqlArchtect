using VisualSqlArchitect.UI.Services;

namespace VisualSqlArchitect.UI.Services.ConnectionManager;

public interface IConnectionProfileStore
{
    IReadOnlyList<ConnectionProfile> LoadProfiles(CredentialVaultStore credentialVault);

    void PersistProfiles(IEnumerable<ConnectionProfile> profiles, CredentialVaultStore credentialVault);
}

