using AkkornStudio.UI.Services.Connection;

namespace AkkornStudio.UI.Services.ConnectionManager;

public interface IConnectionProfileStore
{
    IReadOnlyList<ConnectionProfile> LoadProfiles(CredentialVaultStore credentialVault);

    void PersistProfiles(IEnumerable<ConnectionProfile> profiles, CredentialVaultStore credentialVault);
}

