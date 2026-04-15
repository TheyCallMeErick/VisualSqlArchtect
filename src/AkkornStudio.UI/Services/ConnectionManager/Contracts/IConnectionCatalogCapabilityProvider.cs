using AkkornStudio.Core;

namespace AkkornStudio.UI.Services.ConnectionManager.Contracts;

public interface IConnectionCatalogCapabilityProvider
{
    IReadOnlyList<ConnectionContextLevel> GetSupportedLevels(DatabaseProvider provider);
}
